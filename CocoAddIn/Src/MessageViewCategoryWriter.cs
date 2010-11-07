// Coco/R Custom Tool - Coco/R integration into SharpDevelop
// Copyright (C) 2007  Daniel Grunwald
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
using System.Text;

using ICSharpCode.SharpDevelop.Gui;

namespace Grunwald.CocoAddIn
{
	sealed class MessageViewCategoryWriter : TextWriter
	{
		readonly MessageViewCategory category;
		
		public MessageViewCategoryWriter(MessageViewCategory category)
		{
			if (category == null)
				throw new ArgumentNullException("category");
			this.category = category;
		}
		
		public override Encoding Encoding {
			get {
				return Encoding.UTF8;
			}
		}
		
		public override void Write(char value)
		{
			category.AppendText(value.ToString());
		}
		
		public override void Write(string value)
		{
			if (!string.IsNullOrEmpty(value)) {
				category.AppendText(value);
			}
		}
		
		public override void Write(char[] buffer, int index, int count)
		{
			category.AppendText(new string(buffer, index, count));
		}
		
		public override void WriteLine()
		{
			category.AppendLine("");
		}
		
		public override void WriteLine(string value)
		{
			category.AppendLine(value);
		}
	}
}
