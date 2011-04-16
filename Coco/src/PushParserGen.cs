// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace at.jku.ssw.Coco
{
	public class PushParserGen : AbstractParserGen
	{
		internal bool emitExpectedSets;
		
		public PushParserGen(Parser parser) : base(parser)
		{
		}
		
		public override void WriteParser()
		{
			int oldPos = buffer.Pos;  // Pos is modified by CopySourcePart
			string fr = Path.Combine(tab.srcDir, "PushParser.frame");
			if (!File.Exists(fr)) {
				if (tab.frameDir != null) fr = Path.Combine(tab.frameDir.Trim(), "PushParser.frame");
				if (!File.Exists(fr)) throw new FatalError("Cannot find PushParser.frame");
			}
			try {
				fram = new FileStream(fr, FileMode.Open, FileAccess.Read, FileShare.Read);
			} catch (IOException) {
				throw new FatalError("Cannot open PushParser.frame.");
			}
			OpenGen();
			CopyFramePart("-->begin", false);
			CopySourcePart(tab.copyPos, 0);

			if (preamblePos != null) { CopySourcePart(preamblePos, 0); gen.WriteLine(); }
			CopyFramePart("-->namespace");
			/* AW open namespace, if it exists */
			if (tab.nsName != null && tab.nsName.Length > 0) {
				gen.WriteLine("namespace {0} {{", tab.nsName);
				gen.WriteLine();
			}
			
			GenerateStatusGraph(); OptimizeStatusGraph(); InsertSemanticActionsBeforeAlternatives(); CountIncomingEdgesAndAssignIDs();
			CopyFramePart("-->constants"); WriteNamedStateConstants();
			
			if (emitExpectedSets) {
				EmitCodeForGetFollowSets();
			}
			
			CopyFramePart("-->declarations"); CopySourcePart(semDeclPos, 0);
			
			Indent(1);
			gen.WriteLine("int currentState = " + statusGraphEntryPoint.id + ";");
			CopyFramePart("-->informToken"); GenerateInformToken();
			CopyFramePart("-->initialization"); InitSets();
			
			CopyFramePart("$$$");
			/* AW 2002-12-20 close namespace, if it exists */
			if (tab.nsName != null && tab.nsName.Length > 0) gen.Write("}");
			gen.Close();
			fram.Close();
			buffer.Pos = oldPos;
		}
		
		void WriteNamedStateConstants()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				if (node.type == GenNodeType.NamedState) {
					Indent(1);
					gen.Write("const int ");
					CopySourcePart(node.pos, 0);
					gen.WriteLine(" = " + node.id + ";");
				}
			}
		}
		
		void EmitCodeForGetFollowSets()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			Indent(1); gen.WriteLine("static BitArray GetExpectedSet(int state)");
			Indent(1); gen.WriteLine("{");
			Indent(2); gen.WriteLine("switch (state) {");
			foreach (GenNode node in allGenNodes) {
				if (!node.visited || node.id == int.MaxValue) continue;
				foreach (GenNode node2 in allGenNodes) {
					if (node2.id == int.MaxValue) continue;
					if (Sets.Equals(node.expectedSet, node2.expectedSet)) {
						Indent(3);
						gen.WriteLine("case {0}:", node2.id);
						node2.visited = false;
					}
				}
				if (Sets.Elements(node.expectedSet) <= maxTerm) {
					Indent(4);
					gen.WriteLine("{");
					Indent(5);
					gen.WriteLine("BitArray a = new BitArray({0});", tab.terminals.Count);
					foreach (Symbol sym in tab.terminals) {
						if (node.expectedSet[sym.n]) {
							Indent(5);
							gen.WriteLine("a.Set({0}, true);", sym.n);
						}
					}
					Indent(5);
					gen.WriteLine("return a;");
					Indent(4);
					gen.WriteLine("}");
				} else {
					Indent(4);
					gen.WriteLine("return set[{0}];", NewCondSet(node.expectedSet));
				}
			}
			foreach (GenNode node in allGenNodes)
				node.visited = false; // ensure visited got set to false everywhere (even if id==int.maxvalue)
			Indent(3); gen.WriteLine("default: throw new InvalidOperationException();");
			Indent(2); gen.WriteLine("}");
			Indent(1); gen.WriteLine("}");
		}
		
		enum GenNodeType
		{
			/// <summary>
			/// Puts next on stack and goes to target nonterminal.
			/// </summary>
			CallNonterminal,
			/// <summary>
			/// Consumes a token (symbol).
			/// </summary>
			ConsumeToken,
			/// <summary>
			/// Unconditionally go to next without doing anything.
			/// </summary>
			GoToNext,
			/// <summary>
			/// Executes a piece of code.
			/// </summary>
			SemanticAction,
			/// <summary>
			/// Choose one of two alternatives.
			/// </summary>
			Alternative,
			/// <summary>
			/// Parse error was detected.
			/// </summary>
			Error,
			/// <summary>
			/// A piece of code that is executed on every alternative that has this PossibleSemanticAction in the first set.
			/// </summary>
			PossibleSemanticAction,
			/// <summary>
			/// A named state.
			/// </summary>
			NamedState
		}
		
		sealed class GenNode
		{
			public int id;
			public int incomingEdges;
			public GenNodeType type;
			public GenNode next;
			public Symbol symbol; // terminal symbol for ConsumeToken
			public BitArray matchSet; // used for Alternative
			public GenNode sub; // target if alternative matches; or target to call (CallNonterminal)
			public bool visited;
			public Position pos;
			public BitArray expectedSet; // expected set (first set on this node)
			
			public GenNode() {}
			public GenNode(GenNode input) {
				this.id = input.id;
				this.incomingEdges = input.incomingEdges;
				this.type = input.type;
				this.next = input.next;
				this.symbol = input.symbol;
				this.matchSet = input.matchSet;
				this.sub = input.sub;
				this.pos = input.pos;
				this.expectedSet = input.expectedSet;
			}
		}
		
		GenNode statusGraphEntryPoint;
		
		void GenerateStatusGraph()
		{
			Dictionary<Node, GenNode> nodeToGenNode = new Dictionary<Node, GenNode>();
			foreach (Node node in tab.nodes) {
				nodeToGenNode.Add(node, new GenNode());
			}
			Dictionary<Symbol, GenNode> nonTerminalToGenNode = new Dictionary<Symbol, GenNode>();
			foreach (Symbol sym in tab.nonterminals) {
				if (sym.semPos == null) {
					nonTerminalToGenNode.Add(sym, nodeToGenNode[sym.graph]);
				} else {
					GenNode sem = new GenNode();
					sem.type = GetSemNodeType(sym.semPos);
					sem.next = nodeToGenNode[sym.graph];
					sem.pos = sym.semPos;
					if (emitExpectedSets)
						sem.expectedSet = tab.Expected(sym.graph, sym);
					nonTerminalToGenNode.Add(sym, sem);
				}
			}
			statusGraphEntryPoint = nodeToGenNode[tab.gramSy.graph];
			foreach (Symbol nt in tab.nonterminals) {
				WalkGraph(
					nt.graph, new BitArray(tab.nodes.Count), delegate (Node node) {
						GenNode genNode = nodeToGenNode[node];
						if (emitExpectedSets)
							genNode.expectedSet = tab.Expected(node, nt);
						switch (node.typ) {
							case Node.nt: // call nonterminal
								genNode.type = GenNodeType.CallNonterminal;
								genNode.next = node.next != null ? nodeToGenNode[node.next] : null; // null if tail call
								genNode.sub = nonTerminalToGenNode[node.sym];
								break;
							case Node.any:
							case Node.t: // terminal
								genNode.type = GenNodeType.ConsumeToken;
								genNode.symbol = (node.typ == Node.t) ? node.sym : null;
								genNode.next = node.next != null ? nodeToGenNode[node.next] : null;
								break;
							case Node.eps:
							case Node.expectedConflict:
								genNode.type = GenNodeType.GoToNext;
								genNode.next = node.next != null ? nodeToGenNode[node.next] : null;
								break;
							case Node.sem:
								genNode.type = GetSemNodeType(node.pos);
								genNode.pos = node.pos;
								genNode.next = node.next != null ? nodeToGenNode[node.next] : null;
								break;
							case Node.opt:
							case Node.iter:
								genNode.type = GenNodeType.Alternative;
								genNode.matchSet = tab.First(node.sub);
								genNode.sub = node.sub != null ? nodeToGenNode[node.sub] : null;
								genNode.next = node.next != null ? nodeToGenNode[node.next] : null;
								break;
							case Node.alt:
								genNode.type = GenNodeType.Alternative;
								genNode.matchSet = tab.First(node.sub);
								genNode.sub = node.sub != null ? nodeToGenNode[node.sub] : null;
								if (node.down != null) {
									genNode.next = nodeToGenNode[node.down];
								} else {
									genNode.next = new GenNode();
									if (emitExpectedSets)
										genNode.next.expectedSet = new BitArray(tab.terminals.Count);
									genNode.next.type = GenNodeType.Error;
									genNode.next.next = node.next != null ? nodeToGenNode[node.next] : null;
								}
								break;
							default:
								errors.SemErr("Node type " + node.typ + " not supported in push mode");
								genNode.type = GenNodeType.Error;
								break;
						}
					});
			}
		}
		
		void WalkGraph(Node node, BitArray visited, Action<Node> func)
		{
			if (node == null || visited.Get(node.n))
				return;
			visited.Set(node.n, true);
			func(node);
			WalkGraph(node.next, visited, func);
			WalkGraph(node.down, visited, func);
			WalkGraph(node.sub, visited, func);
		}
		
		GenNodeType GetSemNodeType(Position pos)
		{
			StringWriter w = new StringWriter();
			tab.CopySourcePart(w, pos, 0);
			string text = w.ToString();
			const string possibleSemActionMarker = "OnEachPossiblePath:";
			const string namedStateMarker = "NamedState:";
			if (text.StartsWith(possibleSemActionMarker, StringComparison.Ordinal)) {
				pos.beg += possibleSemActionMarker.Length;
				return GenNodeType.PossibleSemanticAction;
			} else if (text.StartsWith(namedStateMarker, StringComparison.Ordinal)) {
				pos.beg += namedStateMarker.Length;
				return GenNodeType.NamedState;
			} else {
				return GenNodeType.SemanticAction;
			}
		}
		
		static void TraverseStatusGraph(GenNode node, Action<GenNode> action)
		{
			if (node == null || node.visited) return;
			node.visited = true;
			action(node);
			TraverseStatusGraph(node.next, action);
			TraverseStatusGraph(node.sub, action);
		}
		
		void OptimizeStatusGraph()
		{
			OptimizeTailCalls();
			RemoveEpsilonNodes();
			SimplifySingleTokenAlternatives();
			MergeIdenticalNodes();
			CombineAlternativesThatGoToSameNode();
		}
		
		void OptimizeTailCalls()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			// convert tail calls to epsilon nodes
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				if (node.type == GenNodeType.CallNonterminal && node.next == null) {
					node.next = node.sub;
					node.sub = null;
					node.type = GenNodeType.GoToNext;
				}
			}
		}
		
		void RemoveEpsilonNodes()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			// remove epsilon nodes
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				while (node.sub != null && node.sub.type == GenNodeType.GoToNext) {
					node.sub = node.sub.next;
				}
				while (node.next != null && node.next.type == GenNodeType.GoToNext) {
					node.next = node.next.next;
				}
			}
		}
		
		void MergeIdenticalNodes()
		{
			// merge identical nodes
			bool hasChanges;
			do {
				List<GenNode> allGenNodes = new List<GenNode>();
				TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
				Dictionary<GenNode, GenNode> compareDict = new Dictionary<GenNode, GenNode>(new GenNodeEqualityComparer());
				Dictionary<GenNode, GenNode> replaceDict = new Dictionary<GenNode, GenNode>();
				hasChanges = false;
				foreach (GenNode node in allGenNodes) {
					GenNode existing;
					if (compareDict.TryGetValue(node, out existing)) {
						replaceDict.Add(node, existing);
						hasChanges = true;
					} else {
						compareDict.Add(node, node);
						replaceDict.Add(node, node);
					}
				}
				foreach (GenNode node in allGenNodes) {
					node.visited = false;
					if (node.next != null)
						node.next = replaceDict[node.next];
					if (node.sub != null)
						node.sub = replaceDict[node.sub];
				}
			} while (hasChanges);
		}
		
		sealed class GenNodeEqualityComparer : IEqualityComparer<GenNode>
		{
			public bool Equals(GenNode x, GenNode y)
			{
				if (x.type == y.type && x.symbol == y.symbol && x.sub == y.sub && x.pos == y.pos && x.next == y.next) {
					return Sets.Equals(x.expectedSet, y.expectedSet) && Sets.Equals(x.matchSet, y.matchSet);
				} else {
					return false;
				}
			}
			
			public int GetHashCode(GenNode obj)
			{
				int hashCode = 0;
				unchecked {
					hashCode += 1000000009 * obj.type.GetHashCode();
					if (obj.next != null)
						hashCode += 1000000021 * obj.next.GetHashCode();
					if (obj.symbol != null)
						hashCode += 1000000033 * obj.symbol.GetHashCode();
					if (obj.sub != null)
						hashCode += 1000000093 * obj.sub.GetHashCode();
					if (obj.pos != null)
						hashCode += 1000000103 * obj.pos.GetHashCode();
				}
				return hashCode;
			}
		}
		
		void SimplifySingleTokenAlternatives()
		{
			// If the "sub" portion of an alternative expects a single token on all paths and those paths then converge
			// to a single following node, and if there are no semantic actions on that path, then we can simplify and
			// replace all those paths with a single "ANY" node (this works because the parent alternative ensures the first token will match).
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
			}
			foreach (GenNode node in allGenNodes.ToArray()) {
				if (node.type == GenNodeType.Alternative) {
					GenNode followingNode;
					if (IsSingleConsumeTokenFollowedBy(node.sub, out followingNode)) {
						BitArray oldExpected = node.sub.expectedSet;
						node.sub = new GenNode();
						node.sub.type = GenNodeType.ConsumeToken;
						node.sub.next = followingNode;
						node.sub.expectedSet = oldExpected;
						allGenNodes.Add(node.sub);
					}
					foreach (GenNode n in allGenNodes) {
						n.visited = false;
					}
				}
			}
		}
		
		bool IsSingleConsumeTokenFollowedBy(GenNode node, out GenNode followingNode)
		{
			if (node == null || node.visited) {
				followingNode = null;
				return false;
			}
			node.visited = true;
			switch (node.type) {
				case PushParserGen.GenNodeType.CallNonterminal:
					followingNode = node.next;
					GenNode productionEndNode;
					return IsSingleConsumeTokenFollowedBy(node.sub, out productionEndNode) && productionEndNode == null;
				case PushParserGen.GenNodeType.Alternative:
					GenNode node2;
					return IsSingleConsumeTokenFollowedBy(node.next, out followingNode)
						&& IsSingleConsumeTokenFollowedBy(node.sub, out node2)
						&& followingNode == node2;
				case PushParserGen.GenNodeType.GoToNext:
					return IsSingleConsumeTokenFollowedBy(node, out followingNode);
				case PushParserGen.GenNodeType.ConsumeToken:
				case PushParserGen.GenNodeType.Error:
					followingNode = node.next;
					return true;
				default:
					followingNode = null;
					return false;
			}
		}
		
		void CombineAlternativesThatGoToSameNode()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				while (node.type == GenNodeType.Alternative && node.next != null && node.next.type == GenNodeType.Alternative && node.sub == node.next.sub) {
					node.matchSet = new BitArray(node.matchSet).Or(node.next.matchSet);
					node.next = node.next.next;
				}
			}
		}
		
		void InsertSemanticActionsBeforeAlternatives()
		{
			CountIncomingEdgesAndAssignIDs(); // calculate incomingEdges
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			List<GenNode> alternativeNodes = new List<GenNode>();
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				if (node.type == GenNodeType.Alternative && node.incomingEdges > 0) {
					alternativeNodes.Add(node);
				}
			}
			foreach (GenNode alt in alternativeNodes) {
				List<GenNode> psemList = new List<GenNode>();
				FindReachablePossibleSemanticActions(alt, delegate(GenNode n) { if (!psemList.Contains(n)) psemList.Add(n); });
				foreach (GenNode node in allGenNodes) {
					node.visited = false;
				}
				foreach (GenNode psem in psemList) {
					alt.next = new GenNode(alt);
					allGenNodes.Add(alt.next);
					alt.type = GenNodeType.SemanticAction;
					alt.pos = psem.pos;
					alt.sub = null; alt.matchSet = null;
				}
			}
		}
		
		void FindReachablePossibleSemanticActions(GenNode node, Action<GenNode> action)
		{
			if (node == null || node.visited) return;
			node.visited = true;
			switch (node.type) {
				case PushParserGen.GenNodeType.CallNonterminal:
					FindReachablePossibleSemanticActions(node.sub, action);
					break;
				case PushParserGen.GenNodeType.GoToNext:
				case PushParserGen.GenNodeType.SemanticAction:
				case PushParserGen.GenNodeType.NamedState:
					FindReachablePossibleSemanticActions(node.next, action);
					break;
				case PushParserGen.GenNodeType.Alternative:
					FindReachablePossibleSemanticActions(node.next, action);
					FindReachablePossibleSemanticActions(node.sub, action);
					break;
				case PushParserGen.GenNodeType.ConsumeToken:
				case PushParserGen.GenNodeType.Error:
					// stop where token is required
					break;
				case PushParserGen.GenNodeType.PossibleSemanticAction:
					action(node);
					FindReachablePossibleSemanticActions(node.next, action);
					break;
				default:
					throw new NotSupportedException();
			}
		}
		
		void CountIncomingEdgesAndAssignIDs()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			foreach (GenNode node in allGenNodes) {
				node.incomingEdges = 0;
			}
			foreach (GenNode node in allGenNodes) {
				node.visited = false;
				if (node.next != null)
					node.next.incomingEdges++;
				if (node.sub != null)
					node.sub.incomingEdges++;
			}
			statusGraphEntryPoint.incomingEdges++;
			foreach (GenNode node in allGenNodes) {
				// where possible, "inline" node into previous node
				switch (node.type) {
					case GenNodeType.Alternative:
						// Inlining on alternative also allows the next token to be ConsumeToken or Alternative:
						// this is because the "if (la == null)" check was already done as part of the alternative,
						// so we don't need to repeat it.
						if (MayInlineIntoPrevious(node.next, true))
							node.next.incomingEdges = 0;
						if (MayInlineIntoPrevious(node.sub, true))
							node.sub.incomingEdges = 0;
						break;
					case GenNodeType.PossibleSemanticAction:
					case GenNodeType.SemanticAction:
					case GenNodeType.NamedState:
					case GenNodeType.GoToNext:
					case GenNodeType.Error:
						if (MayInlineIntoPrevious(node.next, false))
							node.next.incomingEdges = 0;
						break;
					case GenNodeType.CallNonterminal:
						if (MayInlineIntoPrevious(node.sub, false))
							node.sub.incomingEdges = 0;
						break;
				}
			}
			int nextID = 0;
			foreach (GenNode node in allGenNodes) {
				if (node.incomingEdges == 0)
					node.id = int.MaxValue;
				else
					node.id = nextID++;
			}
		}
		
		bool MayInlineIntoPrevious(GenNode next, bool alreadyHasToken)
		{
			if (next == null || next.incomingEdges != 1)
				return false;
			if (next.type == GenNodeType.NamedState)
				return false;
			return alreadyHasToken || (next.type != GenNodeType.ConsumeToken && next.type != GenNodeType.Alternative);
		}
		
		int indent;
		
		void GenerateInformToken()
		{
			List<GenNode> allGenNodes = new List<GenNode>();
			TraverseStatusGraph(statusGraphEntryPoint, allGenNodes.Add);
			gen.WriteLine("switchlbl: switch (currentState) {");
			indent = 3;
			foreach (GenNode node in allGenNodes) {
				if (node.incomingEdges == 0)
					continue;
				Indent(indent);
				gen.Write("case " + node.id + ": {");
				/*
				foreach (Symbol sym in tab.nonterminals) {
					if (sym.graph == node) {
						gen.Write(" // start of " + sym.name);
						if (sym.semPos != null)
							errors.SemErr(sym.line, sym.semPos.col, "Code blocks in front of '=' not supported in push-mode (in " + sym.name + ")");
					}
				}*/
				gen.WriteLine();
				indent++;
				
				EmitCodeForNode(node);
				Indent(--indent);
				gen.WriteLine("}");
			}
			Indent(--indent);
			gen.WriteLine("}");
		}
		
		void EmitCodeForNode(GenNode node)
		{
			switch (node.type) {
				case GenNodeType.CallNonterminal:
					if (node.next != null) {
						Indent(indent);
						gen.WriteLine("stateStack.Push(" + node.next.id + ");");
					} else {
						// do a 'tail call'
					}
					EmitGoToNode(node.sub);
					break;
				case GenNodeType.ConsumeToken:
					if (node.incomingEdges != 0) {
						Indent(indent);
						gen.WriteLine("if (la == null) { currentState = " + node.id + "; break; }");
					}
					if (node.symbol != null) {
						Indent(indent);
						gen.WriteLine("Expect({0}, la); // {1}", node.symbol.n, node.symbol.name);
					}
					EmitSetStateToNode(node.next);
					Indent(indent);
					gen.WriteLine("break;");
					break;
				case GenNodeType.GoToNext: // epsilon = empty
				case GenNodeType.NamedState:
					EmitGoToNode(node.next);
					break;
				case GenNodeType.Alternative:
					if (node.incomingEdges != 0) {
						Indent(indent);
						gen.WriteLine("if (la == null) { currentState = " + node.id + "; break; }");
					}
					Indent(indent++);
					gen.Write("if (");
					GenCond(node.matchSet);
					gen.WriteLine(") {");
					EmitGoToNode(node.sub);
					Indent(indent - 1);
					gen.WriteLine("} else {");
					EmitGoToNode(node.next);
					Indent(--indent);
					gen.WriteLine("}");
					break;
				case GenNodeType.Error:
					Indent(indent);
					gen.WriteLine("Error(la);");
					EmitGoToNode(node.next);
					break;
				case GenNodeType.PossibleSemanticAction:
				case GenNodeType.SemanticAction:
					StringWriter w = new StringWriter();
					tab.CopySourcePart(w, node.pos, 0);
					bool isControlFlowHack = Regex.IsMatch(w.ToString(), @"(goto \w+|break|return);\s*$");
					CopySourcePart(node.pos, indent);
					if (!isControlFlowHack)
						EmitGoToNode(node.next);
					break;
				default:
					throw new NotSupportedException(node.type.ToString());
			}
		}
		
		void EmitGoToNode(GenNode node)
		{
			if (node == null) {
				Indent(indent);
				gen.WriteLine("currentState = stateStack.Pop();");
				Indent(indent);
				gen.WriteLine("goto switchlbl;");
			} else if (node.incomingEdges == 0) {
				EmitCodeForNode(node);
			} else {
				Indent(indent);
				gen.WriteLine("goto case " + node.id + ";");
			}
		}
		
		void EmitSetStateToNode(GenNode node)
		{
			Indent(indent);
			if (node == null) {
				gen.WriteLine("currentState = stateStack.Pop();");
			} else {
				gen.WriteLine("currentState = " + node.id + ";");
			}
		}
		
		void GenCond(BitArray s) {
			int n = Sets.Elements(s);
			if (n == 0) gen.Write("false"); // happens if an ANY set matches no symbol
			else if (n <= maxTerm)
				foreach (Symbol sym in tab.terminals) {
				if (s[sym.n]) {
					gen.Write("la.{1} == {0}", sym.n, tab.tokenKindFieldName);
					--n;
					if (n > 0) gen.Write(" || ");
				}
			}
			else
				gen.Write("set[{0}].Get(la.{1})", NewCondSet(s), tab.tokenKindFieldName);
		}
	}
}
