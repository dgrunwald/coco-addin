/*---------------------------------------------------------------------------*\
    Compiler Generator Coco/R,
    Copyright (c) 1990, 2004 Hanspeter Moessenboeck, University of Linz
    extended by M. Loeberbauer & A. Woess, Univ. of Linz
    with improvements by Pat Terry, Rhodes University
-------------------------------------------------------------------------------
License
    This file is part of Compiler Generator Coco/R

    This program is free software; you can redistribute it and/or modify it
    under the terms of the GNU General Public License as published by the
    Free Software Foundation; either version 2, or (at your option) any
    later version.

    This program is distributed in the hope that it will be useful, but
    WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
    or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
    for more details.

    You should have received a copy of the GNU General Public License along
    with this program; if not, write to the Free Software Foundation, Inc.,
    59 Temple Place - Suite 330, Boston, MA 02111-1307, USA.

    As an exception, it is allowed to write an extension of Coco/R that is
    used as a plugin in non-free software.

    If not otherwise stated, any source code generated by Coco/R (other than
    Coco/R itself) does not fall under the GNU General Public License.
\*---------------------------------------------------------------------------*/
using System.IO;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;



using System;
using System.Collections;

namespace Grunwald.CocoAddIn.CocoParser {


// ----------------------------------------------------------------------------
// Parser
// ----------------------------------------------------------------------------
//! A Coco/R Parser
public partial class SimpleCocoParser
{
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _number = 2;
	public const int _string = 3;
	public const int _badString = 4;
	public const int _char = 5;
	public const int maxT = 49;  //<! max term (w/o pragmas)
	public const int _ddtSym = 50;
	public const int _directive = 51;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;

	public Scanner scanner;
	public Errors  errors;

	public Token t;    //!< last recognized token
	public Token la;   //!< lookahead token
	int errDist = minErrDist;



	public SimpleCocoParser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}

	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }
				if (la.kind == 50) {
				}
				if (la.kind == 51) {
				}

			la = t;
		}
	}

	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}

	bool StartOf (int s) {
		return set[s].Get(la.kind);
	}

	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol].Get(kind) || set[repFol].Get(kind) || set[0].Get(kind))) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}


	void SimpleCoco() {
		parserName = "DummyParser";
		if (la.kind == 6) {
			Get();
			copySection = new TextSegment() { StartOffset = t.pos };
			while (StartOf(1)) {
				Get();
			}
			Expect(7);
			copySection.EndOffset = t.pos + t.val.Length;
		}
		if (StartOf(2)) {
			Get();
			usingSection = new TextSegment() { StartOffset = t.pos };
			while (StartOf(3)) {
				Get();
			}
			usingSection.EndOffset = t.pos + 1;
		}
		parserSection = new TextSegment() { StartOffset = la.pos };
		if (la.kind == 8) {
			Get();
		} else if (la.kind == 9) {
			Get();
			while (la.kind == 10) {
				Get();
				Expect(11);
			}
		} else SynErr(50);
		Expect(1);
		parserName = t.val;
		if (StartOf(4)) {
			Get();
			while (StartOf(4)) {
				Get();
			}
		}
		if (la.kind == 12) {
			Get();
		}
		if (la.kind == 13) {
			ListInfo chars = new ListInfo() {
			  	name = "CHARACTERS",
			  	segment = new TextSegment() { StartOffset = la.pos }
			};
			Get();
			while (la.kind == 1) {
				SetDecl();
			}
			chars.segment.EndOffset = t.pos + (t.val != null  ? t.val.Length : 1); lists.Add(chars);
		}
		if (la.kind == 14) {
			ListInfo toks = new ListInfo() {
			  	name = "TOKENS",
			  	segment = new TextSegment() { StartOffset = la.pos }
			};
			Get();
			while (la.kind == 1 || la.kind == 3 || la.kind == 5) {
				TokenDecl();
			}
			toks.segment.EndOffset = t.pos + (t.val != null  ? t.val.Length : 1); lists.Add(toks);
		}
		if (la.kind == 15) {
			ListInfo prags = new ListInfo() {
			  	name = "PRAGMAS",
			  	segment = new TextSegment() { StartOffset = la.pos }
			};
			Get();
			while (la.kind == 1 || la.kind == 3 || la.kind == 5) {
				TokenDecl();
			}
			prags.segment.EndOffset = t.pos + (t.val != null  ? t.val.Length : 1); lists.Add(prags);
		}
		while (la.kind == 16) {
			Get();
			Expect(17);
			TokenExpr();
			Expect(18);
			TokenExpr();
			if (la.kind == 19) {
				Get();
			}
		}
		while (la.kind == 20) {
			Get();
			Set();
		}
		while (!(la.kind == 0 || la.kind == 21)) {SynErr(51); Get();}
		Expect(21);
		while (la.kind == 1) {
			ListInfo prod = new ListInfo() {
			  	name = la.val,
			  	segment = new TextSegment() { StartOffset = la.pos }
			};
			Get();
			if (la.kind == 29 || la.kind == 31) {
				AttrDecl();
			}
			if (la.kind == 47) {
				SemText();
			}
			ExpectWeak(22, 5);
			Expression();
			ExpectWeak(23, 6);
			prod.segment.EndOffset = t.pos + (t.val != null  ? t.val.Length : 1); productions.Add(prod);
		}
		Expect(24);
		Expect(1);
		Expect(23);
		parserSection.EndOffset = t.pos + 1;
	}

	void SetDecl() {
		Expect(1);
		Expect(22);
		Set();
		Expect(23);
	}

	void TokenDecl() {
		Sym();
		while (!(StartOf(7))) {SynErr(52); Get();}
		if (la.kind == 22) {
			Get();
			TokenExpr();
			Expect(23);
		}
		if (la.kind == 47) {
			SemText();
		}
	}

	void TokenExpr() {
		TokenTerm();
		while (WeakSeparator(33,8,9) ) {
			TokenTerm();
		}
	}

	void Set() {
		SimSet();
		while (la.kind == 25 || la.kind == 26) {
			if (la.kind == 25) {
				Get();
				SimSet();
			} else {
				Get();
				SimSet();
			}
		}
	}

	void AttrDecl() {
		if (la.kind == 29) {
			Get();
			while (StartOf(10)) {
				if (StartOf(11)) {
					Get();
				} else {
					Get();
				}
			}
			Expect(30);
		} else if (la.kind == 31) {
			Get();
			while (StartOf(12)) {
				if (StartOf(13)) {
					Get();
				} else {
					Get();
				}
			}
			Expect(32);
		} else SynErr(53);
	}

	void SemText() {
		Expect(47);
		while (StartOf(14)) {
			if (StartOf(15)) {
				Get();
			} else if (la.kind == 4) {
				Get();
			} else {
				Get();
			}
		}
		Expect(48);
	}

	void Expression() {
		Term();
		while (WeakSeparator(33,16,17) ) {
			Term();
		}
	}

	void SimSet() {
		if (la.kind == 1) {
			Get();
		} else if (la.kind == 3) {
			Get();
		} else if (la.kind == 5) {
			Char();
			if (la.kind == 27) {
				Get();
				Char();
			}
		} else if (la.kind == 28) {
			Get();
		} else SynErr(54);
	}

	void Char() {
		Expect(5);
	}

	void Sym() {
		if (la.kind == 1) {
			Get();
		} else if (la.kind == 3 || la.kind == 5) {
			if (la.kind == 3) {
				Get();
			} else {
				Get();
			}
		} else SynErr(55);
	}

	void Term() {
		if (StartOf(18)) {
			if (la.kind == 43 || la.kind == 44) {
				if (la.kind == 43) {
					Resolver();
				} else {
					ExpectedConflict();
				}
			}
			Factor();
			while (StartOf(19)) {
				Factor();
			}
		} else if (StartOf(20)) {
		} else SynErr(56);
	}

	void Resolver() {
		Expect(43);
		Expect(36);
		Condition();
	}

	void ExpectedConflict() {
		Expect(44);
		Expect(36);
		ConflictSymbol();
		while (la.kind == 45) {
			Get();
			ConflictSymbol();
		}
		Expect(37);
	}

	void Factor() {
		switch (la.kind) {
		case 1: case 3: case 5: case 34: case 35: {
			if (la.kind == 34) {
				Get();
			}
			if (la.kind == 35) {
				Get();
			}
			Sym();
			if (la.kind == 29 || la.kind == 31) {
				Attribs();
			}
			break;
		}
		case 36: {
			Get();
			Expression();
			Expect(37);
			break;
		}
		case 38: {
			Get();
			Expression();
			Expect(39);
			break;
		}
		case 40: {
			Get();
			Expression();
			Expect(41);
			break;
		}
		case 47: {
			SemText();
			break;
		}
		case 28: {
			Get();
			break;
		}
		case 42: {
			Get();
			break;
		}
		default: SynErr(57); break;
		}
	}

	void Attribs() {
		if (la.kind == 29) {
			Get();
			while (StartOf(10)) {
				if (StartOf(11)) {
					Get();
				} else {
					Get();
				}
			}
			Expect(30);
		} else if (la.kind == 31) {
			Get();
			while (StartOf(12)) {
				if (StartOf(13)) {
					Get();
				} else {
					Get();
				}
			}
			Expect(32);
		} else SynErr(58);
	}

	void Condition() {
		while (StartOf(21)) {
			if (la.kind == 36) {
				Get();
				Condition();
			} else {
				Get();
			}
		}
		Expect(37);
	}

	void ConflictSymbol() {
		Sym();
	}

	void TokenTerm() {
		TokenFactor();
		while (StartOf(8)) {
			TokenFactor();
		}
		if (la.kind == 46) {
			Get();
			Expect(36);
			TokenExpr();
			Expect(37);
		}
	}

	void TokenFactor() {
		if (la.kind == 1 || la.kind == 3 || la.kind == 5) {
			Sym();
		} else if (la.kind == 36) {
			Get();
			TokenExpr();
			Expect(37);
		} else if (la.kind == 38) {
			Get();
			TokenExpr();
			Expect(39);
		} else if (la.kind == 40) {
			Get();
			TokenExpr();
			Expect(41);
		} else SynErr(59);
	}



	public void Parse() {
		la = new Token();
		la.val = "";
		Get();
		SimpleCoco();
		Expect(0); // expect end-of-file automatically added

	}

	static readonly BitArray[] set = {
		new BitArray(new int[] {7438379, 32768}),
		new BitArray(new int[] {-130, -1}),
		new BitArray(new int[] {-834, -1}),
		new BitArray(new int[] {-770, -1}),
		new BitArray(new int[] {-3272706, -1}),
		new BitArray(new int[] {284262443, 40286}),
		new BitArray(new int[] {24215595, 32768}),
		new BitArray(new int[] {7438379, 32768}),
		new BitArray(new int[] {42, 336}),
		new BitArray(new int[] {12386304, 672}),
		new BitArray(new int[] {-1073741826, -1}),
		new BitArray(new int[] {-1073741842, -1}),
		new BitArray(new int[] {-2, -2}),
		new BitArray(new int[] {-18, -2}),
		new BitArray(new int[] {-2, -65537}),
		new BitArray(new int[] {-18, -98305}),
		new BitArray(new int[] {276824106, 40958}),
		new BitArray(new int[] {8388608, 672}),
		new BitArray(new int[] {268435498, 40284}),
		new BitArray(new int[] {268435498, 34140}),
		new BitArray(new int[] {8388608, 674}),
		new BitArray(new int[] {-2, -33})

	};
} // end Parser


//-----------------------------------------------------------------------------
// Errors
//-----------------------------------------------------------------------------
//! Parser error handling
public class Errors
{
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text
	public int firstErrorLine = -1, firstErrorColumn = -1;

	static string strerror(int n) {
		switch (n) {
			case 0: return "EOF expected";
			case 1: return "ident expected";
			case 2: return "number expected";
			case 3: return "string expected";
			case 4: return "badString expected";
			case 5: return "char expected";
			case 6: return "\"[copy]\" expected";
			case 7: return "\"[/copy]\" expected";
			case 8: return "\"COMPILER\" expected";
			case 9: return "\"PUSHCOMPILER\" expected";
			case 10: return "\"WITH\" expected";
			case 11: return "\"EXPECTEDSETS\" expected";
			case 12: return "\"IGNORECASE\" expected";
			case 13: return "\"CHARACTERS\" expected";
			case 14: return "\"TOKENS\" expected";
			case 15: return "\"PRAGMAS\" expected";
			case 16: return "\"COMMENTS\" expected";
			case 17: return "\"FROM\" expected";
			case 18: return "\"TO\" expected";
			case 19: return "\"NESTED\" expected";
			case 20: return "\"IGNORE\" expected";
			case 21: return "\"PRODUCTIONS\" expected";
			case 22: return "\"=\" expected";
			case 23: return "\".\" expected";
			case 24: return "\"END\" expected";
			case 25: return "\"+\" expected";
			case 26: return "\"-\" expected";
			case 27: return "\"..\" expected";
			case 28: return "\"ANY\" expected";
			case 29: return "\"<\" expected";
			case 30: return "\">\" expected";
			case 31: return "\"<.\" expected";
			case 32: return "\".>\" expected";
			case 33: return "\"|\" expected";
			case 34: return "\"WEAK\" expected";
			case 35: return "\"GREEDY\" expected";
			case 36: return "\"(\" expected";
			case 37: return "\")\" expected";
			case 38: return "\"[\" expected";
			case 39: return "\"]\" expected";
			case 40: return "\"{\" expected";
			case 41: return "\"}\" expected";
			case 42: return "\"SYNC\" expected";
			case 43: return "\"IF\" expected";
			case 44: return "\"EXPECTEDCONFLICT\" expected";
			case 45: return "\",\" expected";
			case 46: return "\"CONTEXT\" expected";
			case 47: return "\"(.\" expected";
			case 48: return "\".)\" expected";
			case 49: return "??? expected";
			case 50: return "invalid SimpleCoco";
			case 51: return "this symbol not expected in SimpleCoco";
			case 52: return "this symbol not expected in TokenDecl";
			case 53: return "invalid AttrDecl";
			case 54: return "invalid SimSet";
			case 55: return "invalid Sym";
			case 56: return "invalid Term";
			case 57: return "invalid Factor";
			case 58: return "invalid Attribs";
			case 59: return "invalid TokenFactor";

			default: return "error " + n;
		}
	}

	public void SynErr (int line, int col, int n) {
		WriteError(line, col, strerror(n));
	}

	public void SemErr (int line, int col, string s) {
		WriteError(line, col, s);
	}

	public void SemErr (string s) {
		WriteError(0, 0, s);
	}

	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}

	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
	
	public virtual void WriteError(int line, int col, string message) {
		if (line > 0)
			errorStream.WriteLine(errMsgFormat, line, col, message);
		else
			errorStream.WriteLine(message);
		count++;
		if (firstErrorLine == -1 && firstErrorColumn == -1) {
			firstErrorLine = line;
			firstErrorColumn = col;
		}
	}
} // Errors


//-----------------------------------------------------------------------------
// FatalError
//-----------------------------------------------------------------------------
//! Parser fatal error handling
public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}


// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * //

} // end namespace
