// Coco/R Custom Tool - Coco/R integration into SharpDevelop
// Copyright (C) 2010 Siegfried Pammer
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
