// Coco/R Custom Tool - Coco/R integration into SharpDevelop
// Copyright (C) 2007-2010 Daniel Grunwald
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

using System;
using System.IO;
using System.Linq;

using at.jku.ssw.Coco;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;

namespace Grunwald.CocoAddIn
{
	/// <summary>
	/// Runs Coco as custom tool.
	/// </summary>
	public class CocoTool : ICustomTool
	{
		public static string CocoDirectory {
			get {
				return Path.GetDirectoryName(typeof(ShowCocoHelpCommand).Assembly.Location);
			}
		}
		
		static MessageViewCategory cocoCategory;
		
		public static MessageViewCategory CocoCategory {
			get {
				if (cocoCategory == null) {
					cocoCategory = new MessageViewCategory("Coco/R", "Coco/R");
					CompilerMessageView compilerMessageView = (CompilerMessageView)WorkbenchSingleton.Workbench.GetPad(typeof(CompilerMessageView)).PadContent;
					compilerMessageView.AddCategory(cocoCategory);
				}
				return cocoCategory;
			}
		}
		
		/// <summary>
		/// Called by SharpDevelop when your tool has to generate code.
		/// </summary>
		/// <param name="item">
		/// The file for which your tool should generate code.
		/// </param>
		public void GenerateCode(FileProjectItem item, CustomToolContext context)
		{
			string dir = Path.GetDirectoryName(item.FileName);
			string traceFile = Path.Combine(dir, "trace.txt");
			string parserOutFile = Path.Combine(dir, "Parser.cs");
			string scannerOutFile = Path.Combine(dir, "Scanner.cs");
			
			ErrorsInTaskService.ClearAllErrors();
			CocoCategory.ClearText();
			CocoCategory.AppendLine("Generating " + Path.GetFileName(item.FileName) + "...");
			
			using (FileStream inputStream = File.OpenRead(item.FileName)) {
				Scanner scanner = new Scanner(inputStream);
				Parser parser = new Parser(scanner);
				parser.errors = new ErrorsInTaskService(item.FileName);
				parser.errors.errorStream = new MessageViewCategoryWriter(CocoCategory);
				parser.tab = new Tab(parser);
				parser.tab.trace = new LazyTextWriter(delegate { return new StreamWriter(traceFile); } );
				parser.dfa = new DFA(parser);
				parser.pgen = new ParserGen(parser);
				parser.tab.srcName = item.FileName;
				parser.tab.srcDir = Path.GetDirectoryName(item.FileName);
				parser.tab.nsName = context.OutputNamespace;
				parser.tab.frameDir = parser.tab.srcDir;
				parser.tab.outDir = parser.tab.srcDir;
				try {
					parser.Parse();
				} catch (FatalError err) {
					CocoCategory.AppendLine("Fatal error: " + err.Message);
					return;
				} finally {
					parser.tab.trace.Close();
				}
				
				CocoCategory.AppendLine("Done. " + parser.errors.count + " error(s).");
			}
			
			if (File.Exists(traceFile)) {
				context.EnsureOutputFileIsInProject(item, traceFile);
			}
			if (File.Exists(parserOutFile)) {
				context.EnsureOutputFileIsInProject(item, parserOutFile);
			}
			if (File.Exists(scannerOutFile)) {
				context.EnsureOutputFileIsInProject(item, scannerOutFile);
			}
		}
	}
}
