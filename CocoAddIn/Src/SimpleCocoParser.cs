// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Siegfried Pammer" email="siegfriedpammer@gmail.com" />
//     <version>$Revision$</version>
// </file>
using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;

namespace Grunwald.CocoAddIn.CocoParser
{
	/// <summary>
	/// Description of SimpleCocoParser.
	/// </summary>
	public partial class SimpleCocoParser
	{
		TextSegment copySection;
		
		public TextSegment CopySection {
			get { return copySection; }
		}
		
		TextSegment usingSection;
		
		public TextSegment UsingSection {
			get { return usingSection; }
		}
		
		TextSegment parserSection;
		
		public TextSegment ParserSection {
			get { return parserSection; }
		}
		
		string parserName;
		
		public string ParserName {
			get { return parserName; }
		}
		
		public struct ListInfo {
			public TextSegment segment;
			public string name;
		}
		
		List<ListInfo> lists = new List<ListInfo>();
		
		public List<ListInfo> Lists {
			get { return lists; }
		}
		
		List<ListInfo> productions = new List<ListInfo>();
		
		public List<ListInfo> Productions {
			get { return productions; }
		}
	}
}
