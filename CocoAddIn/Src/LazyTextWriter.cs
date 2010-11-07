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
using System.Text;
	
namespace Grunwald.CocoAddIn
{
	sealed class LazyTextWriter : TextWriter
	{
		TextWriter writer;
		Func<TextWriter> writerProvider;
		
		private TextWriter Writer {
			get {
				if (writer == null) {
					writer = writerProvider();
					writerProvider = null;
				}
				return writer;
			}
		}
		
		public LazyTextWriter(Func<TextWriter> writerProvider)
		{
			if (writerProvider == null)
				throw new ArgumentNullException("writerProvider");
			this.writerProvider = writerProvider;
		}
		
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing) {
				if (writer != null) {
					writer.Dispose();
				}
			}
		}
		
		public override void Flush()
		{
			if (writer != null)
				writer.Flush();
		}
		
		public override void Write(string value)
		{
			Writer.Write(value);
		}
		
		public override void Write(char[] buffer, int index, int count)
		{
			Writer.Write(buffer, index, count);
		}
		
		public override void WriteLine()
		{
			Writer.WriteLine();
		}
		
		public override void WriteLine(object value)
		{
			Writer.WriteLine(value);
		}
		
		public override void WriteLine(string value)
		{
			Writer.WriteLine(value);
		}
		
		public override Encoding Encoding {
			get {
				if (writer != null)
					return writer.Encoding;
				else
					return Encoding.Default;
			}
		}
	}
}
