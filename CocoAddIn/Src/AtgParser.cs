// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Siegfried Pammer" email="siegfriedpammer@gmail.com" />
//     <version>$Revision$</version>
// </file>
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Grunwald.CocoAddIn.CocoParser;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Project;

namespace Grunwald.CocoAddIn
{
	/// <summary>
	/// Description of AtgParser.
	/// </summary>
	public class AtgParser : IParser
	{
		public AtgParser()
		{
		}
		
		public string[] LexerTags {
			get { return new string[0]; }
			set { }
		}
		
		public LanguageProperties Language {
			get {
				return LanguageProperties.None;
			}
		}
		
		public IExpressionFinder CreateExpressionFinder(string fileName)
		{
			return null;
		}
		
		public bool CanParse(string fileName)
		{
			return ".atg".Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase);
		}
		
		public bool CanParse(IProject project)
		{
			return false;
		}
		
		List<int> lineOffsets;
		
		void Init(string text)
		{
			lineOffsets = new List<int>();
			lineOffsets.Add(0);
			for (int i = 0; i < text.Length; i++) {
				if (text[i] == '\n') {
					lineOffsets.Add(i + 1);
				} else if (text[i] == '\r') {
					if (i + 1 < text.Length && text[i + 1] != '\n') {
						lineOffsets.Add(i + 1);
					}
				}
			}
		}
		
		int LocationToOffset(Location location)
		{
			if (location.Line <= 0) return -1;
			return lineOffsets[location.Line - 1] + location.Column - 1;
		}
		
		struct Location
		{
			int column;
			
			public int Column {
				get { return column; }
				set { column = value; }
			}
			
			int line;
			
			public int Line {
				get { return line; }
				set { line = value; }
			}
			
			public Location(int column, int line)
			{
				this.column = column;
				this.line = line;
			}
		}
		
		Location OffsetToLocation(int offset)
		{
			int lineNumber = lineOffsets.BinarySearch(offset);
			if (lineNumber < 0) {
				lineNumber = (~lineNumber) - 1;
			}
			return new Location(offset - lineOffsets[lineNumber] + 1, lineNumber + 1);
		}
		
		public ICompilationUnit Parse(IProjectContent projectContent, string fileName, ITextBuffer fileContent)
		{
			Init(fileContent.Text);
			
			SimpleCocoParser parser = new SimpleCocoParser(new Scanner(new StringStream(fileContent.Text)));
			
			parser.Parse();
			
			DefaultCompilationUnit cu = new DefaultCompilationUnit(projectContent);
			
			Location start, end;
			
			if (parser.CopySection != null) {
				start = OffsetToLocation(parser.CopySection.StartOffset);
				end = OffsetToLocation(parser.CopySection.EndOffset);
				
				cu.FoldingRegions.Add(new FoldingRegion("[copy]", new DomRegion(start.Line, start.Column, end.Line, end.Column)));
			}
			
			if (parser.UsingSection != null) {
				start = OffsetToLocation(parser.UsingSection.StartOffset);
				end = OffsetToLocation(parser.UsingSection.EndOffset);
				
				cu.FoldingRegions.Add(new FoldingRegion("[...]", new DomRegion(start.Line, start.Column, end.Line, end.Column)));
			}
			
			DefaultClass parserClass = null;
			
			if (parser.ParserSection != null) {
				start = OffsetToLocation(parser.ParserSection.StartOffset);
				end = OffsetToLocation(parser.ParserSection.EndOffset);
				
				parserClass = new DefaultClass(cu, parser.ParserName);
				
				parserClass.ClassType = ClassType.Class;
				parserClass.Modifiers = ModifierEnum.None;
				parserClass.Region = new DomRegion(start.Line, start.Column, end.Line, end.Column);
				
				cu.Classes.Add(parserClass);
				
				foreach (var info in parser.Lists) {
					start = OffsetToLocation(info.segment.StartOffset);
					end = OffsetToLocation(info.segment.EndOffset);
					
					var region = new DomRegion(start.Line, start.Column, start.Line, start.Column + info.name.Length);
					var body = new DomRegion(start.Line, start.Column, end.Line, end.Column);
					
					var prop = new DefaultProperty(parserClass, info.name) {
						Region = region, BodyRegion = body, Modifiers = ModifierEnum.Public
					};
					
					parserClass.Properties.Add(prop);
				}
				
				foreach (var info in parser.Productions) {
					start = OffsetToLocation(info.segment.StartOffset);
					end = OffsetToLocation(info.segment.EndOffset);
					 
					var region = new DomRegion(start.Line, start.Column, start.Line, start.Column + info.name.Length);
					var body = new DomRegion(start.Line, start.Column, end.Line, end.Column);
					
					var method = new DefaultMethod(parserClass, info.name) {
						Region = region, BodyRegion = body, Modifiers = ModifierEnum.Public
					};
					
					parserClass.Methods.Add(method);
				}
			}
			return cu;
		}
		
		public IResolver CreateResolver()
		{
			return null;
		}
	}
	
	class StringStream : Stream
	{
		StringBuilder builder;
		long currentPos;
		
		public StringStream(string data)
		{
			builder = new StringBuilder(data);
		}
		
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}
		
		public override void SetLength(long value)
		{
			builder.Length = (int)value;
		}
		
		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin) {
				case SeekOrigin.Begin:
					currentPos = offset;
					break;
				case SeekOrigin.Current:
					currentPos += offset;
					break;
				case SeekOrigin.End:
					currentPos = builder.Length - offset;
					break;
				default:
					throw new Exception("Invalid value for SeekOrigin");
			}
			
			return currentPos;
		}
		
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException("buffer");
			
			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException("offset", offset, "Value must be between 0 and " + buffer.Length);

			if (offset + count > buffer.Length)
				throw new ArgumentOutOfRangeException("count", count, "Value must be between 0 and " + (offset + buffer.Length));
			
			string data = builder.ToString().Substring((int)currentPos);
			byte[] bytes = Encoding.ASCII.GetBytes(data);
			int readCount = 0;
			
			for (int i = offset; i < offset + count; i++) {
				if (i >= bytes.Length)
					buffer[i] = 0;
				else {
					buffer[i] = bytes[i - offset];
					readCount++;
				}
			}
			
			return readCount;
		}
		
		public override long Position {
			get { return currentPos; }
			set { currentPos = value; }
		}
		
		public override long Length {
			get { return builder.Length; }
		}
		
		public override void Flush()
		{
		}
		
		public override bool CanWrite {
			get { return false; }
		}
		
		public override bool CanSeek {
			get { return true; }
		}
		
		public override bool CanRead {
			get { return true; }
		}
	}

}
