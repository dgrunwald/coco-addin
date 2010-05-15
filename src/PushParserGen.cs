// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace at.jku.ssw.Coco
{
	public class PushParserGen : AbstractParserGen
	{
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
			CopyFramePart("-->constants");
			CopyFramePart("-->declarations"); CopySourcePart(semDeclPos, 0);
			gen.WriteLine("int currentState = " + GetNodeID(tab.gramSy.graph) + ";");
			CopyFramePart("-->informToken"); GenerateInformToken();
			CopyFramePart("-->initialization"); InitSets();
			
			CopyFramePart("$$$");
			/* AW 2002-12-20 close namespace, if it exists */
			if (tab.nsName != null && tab.nsName.Length > 0) gen.Write("}");
			gen.Close();
			buffer.Pos = oldPos;
		}
		
		static int GetNodeID(Node node)
		{
			return node.n;
		}
		
		int indent;
		
		void GenerateInformToken()
		{
			gen.WriteLine("switchlbl: switch (currentState) {");
			indent = 3;
			foreach (Node node in tab.nodes) {
				Indent(indent);
				gen.Write("case " + GetNodeID(node) + ": {");
				//if (node.line > 0)
				//	gen.Write(" // line " + node.line);
				foreach (Symbol sym in tab.nonterminals) {
					if (sym.graph == node) {
						gen.Write(" // start of " + sym.name);
						if (sym.semPos != null)
							errors.SemErr(sym.line, sym.semPos.col, "Code blocks in front of '=' not supported in push-mode (in " + sym.name + ")");
					}
				}
				gen.WriteLine();
				indent++;
				
				switch (node.typ) {
					case Node.nt: // call nonterminal
						if (node.next != null) {
							Indent(indent);
							gen.WriteLine("stateStack.Push(" + GetNodeID(node.next) + ");");
						} else {
							// do a 'tail call'
						}
						Indent(indent);
						gen.WriteLine("goto case " + GetNodeID(node.sym.graph) + "; // " + node.sym.name);
						break;
					case Node.any:
					case Node.t: // terminal
						if (node.typ == Node.t) {
							Indent(indent);
							gen.WriteLine("Expect({0}, t); // {1}", node.sym.n, node.sym.name);
						}
						EmitSetStateToNode(node.next);
						Indent(indent);
						gen.WriteLine("break;");
						break;
					case Node.eps: // epsilon = empty
						EmitGoToNode(node.next);
						break;
					case Node.iter:
					case Node.opt:
						Indent(indent++);
						gen.Write("if (");
						GenCond(tab.First(node.sub));
						gen.WriteLine(") {");
						EmitGoToNode(node.sub);
						Indent(indent - 1);
						gen.WriteLine("} else {");
						EmitGoToNode(node.next);
						Indent(--indent);
						gen.WriteLine("}");
						break;
					case Node.alt:
						Indent(indent++);
						gen.Write("if (");
						GenCond(tab.First(node.sub));
						gen.WriteLine(") {");
						EmitGoToNode(node.sub);
						Indent(indent - 1);
						gen.WriteLine("} else {");
						if (node.down != null) {
							EmitGoToNode(node.down);
						} else {
							Indent(indent);
							gen.WriteLine("Error(t);");
							EmitGoToNode(node.next);
						}
						Indent(--indent);
						gen.WriteLine("}");
						break;
					case Node.sem:
						CopySourcePart(node.pos, indent);
						EmitGoToNode(node.next);
						break;
					default:
						errors.SemErr("Node type " + node.typ + " not supported in push mode");
						gen.WriteLine("   throw new Exception(\"unknown node type " + node.typ + "\");");
						break;
				}
				Indent(--indent);
				gen.WriteLine("}");
			}
			Indent(--indent);
			gen.WriteLine("}");
		}
		
		void EmitGoToNode(Node node)
		{
			if (node == null) {
				Indent(indent);
				gen.WriteLine("currentState = stateStack.Pop();");
				Indent(indent);
				gen.WriteLine("goto switchlbl;");
			} else {
				Indent(indent);
				gen.WriteLine("goto case " + GetNodeID(node) + ";");
			}
		}
		
		void EmitSetStateToNode(Node node)
		{
			Indent(indent);
			if (node == null) {
				gen.WriteLine("currentState = stateStack.Pop();");
			} else {
				gen.WriteLine("currentState = " + GetNodeID(node) + ";");
			}
		}
		
		void GenCond(BitArray s) {
			int n = Sets.Elements(s);
			if (n == 0) gen.Write("false"); // happens if an ANY set matches no symbol
			else if (n <= maxTerm)
				foreach (Symbol sym in tab.terminals) {
				if (s[sym.n]) {
					gen.Write("t.kind == {0}", sym.n);
					--n;
					if (n > 0) gen.Write(" || ");
				}
			}
			else
				gen.Write("set[{0}, t.kind]", NewCondSet(s));
		}
	}
}
