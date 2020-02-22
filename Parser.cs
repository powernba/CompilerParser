using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace MTProject
{
	public class Parser
	{
		private Scanner scanner;
		private Emit emit;
		private Table symbolTable;
		private Token token;
		private Diagnostics diag;
		
		private Stack<Label> breakStack = new Stack<Label>();
		private Stack<Label> continueStack = new Stack<Label>();

		
		public Parser(Scanner scanner, Emit emit, Table symbolTable, Diagnostics diag)
		{
			this.scanner = scanner;
			this.emit = emit;
			this.symbolTable = symbolTable;
			this.diag = diag;
		}
		
		public void AddPredefinedSymbols()
		{
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1,-1, "int"), typeof(System.Int32)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1,-1, "bool"), typeof(System.Boolean)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1,-1, "double"), typeof(System.Double)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1,-1, "char"), typeof(System.Char)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1,-1, "string"), typeof(System.String)));
		}
		
		public bool Parse()
		{
			ReadNextToken();
			AddPredefinedSymbols();
			return IsProgram() && token is EOFToken;
		}
		
		public void ReadNextToken()
		{
			token = scanner.Next();
		}
		
		public bool CheckKeyword(string keyword)
		{
			bool result = (token is KeywordToken) && ((KeywordToken)token).value==keyword;
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckSpecialSymbol(string symbol)
		{
			bool result = (token is SpecialSymbolToken) && ((SpecialSymbolToken)token).value==symbol;
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckIdent()
		{
			bool result = (token is IdentToken);
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckNumber()
		{
			bool result = (token is NumberToken);
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckDouble()
		{
			bool result = (token is DoubleToken);
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckBoolean()
		{
			bool result = (token is BooleanToken);
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckChar()
		{
			bool result = (token is CharToken);
			if (result) ReadNextToken();
			return result;
		}
		
		public bool CheckString()
		{
			bool result = (token is StringToken);
			if (result) ReadNextToken();
			return result;
		}
		
		void SkipUntilSemiColon() {
			Token Tok;
			do {
				Tok = scanner.Next();
			} while (!((Tok is EOFToken) ||
				  	   (Tok is SpecialSymbolToken) && ((Tok as SpecialSymbolToken).value == ";")));
		}
		
		public void Error(string message)
		{
			diag.Error(token.line, token.column, message);
			SkipUntilSemiColon();
		}
		
		public void Error(string message, Token token)
		{
			diag.Error(token.line, token.column, message);
			SkipUntilSemiColon();
		}
		
		public void Error(string message, Token token, params object[] par)
		{
			diag.Error(token.line, token.column, string.Format(message, par));
			SkipUntilSemiColon();
		}
		
		public void Warning(string message)
		{
			diag.Warning(token.line, token.column, message);
		}
		
		public void Warning(string message, Token token)
		{
			diag.Warning(token.line, token.column, message);
		}
		
		public void Warning(string message, Token token, params object[] par)
		{
			diag.Warning(token.line, token.column, string.Format(message, par));
		}
		
		public void Note(string message)
		{
			diag.Note(token.line, token.column, message);
		}
		
		public void Note(string message, Token token)
		{
			diag.Note(token.line, token.column, message);
		}
		
		public void Note(string message, Token token, params object[] par)
		{
			diag.Note(token.line, token.column, string.Format(message, par));
		}

		//[1]  Program = {Statement}.
		public bool IsProgram()
		{
			//IdentToken id = token as IdentToken;
			//symbolTable.AddToUniverse(new PrimitiveTypeSymbol(id, emit.InitProgramClass(id.value)));
			while (IsStatement()) ;

			return diag.GetErrorCount() == 0;
		}

		//[2]  Statement = CompoundSt | IfSt | WhileSt | StopSt | [Expression] ';'.
		public bool IsStatement()
		{
			if (!IsCompoundSt() || !IsIfSt() || !IsWhileSt() || !IsStopSt() || !IsExpression())
			{
				return false;
			}

			if (!CheckSpecialSymbol(";"))
			{
				Error("Очаквам специален символ ';'");
				return false;
			}
			return true;
		}

		//[3]  CompoundSt = '{' {Declaration} {Statement} '}'
		public bool IsCompoundSt()
		{
			if (!CheckSpecialSymbol("{"))
			{ 
				Error("Очаквам специален символ '{'");
				return false;
			}
			while (IsDeclaration()) ;
			while (IsStatement()) ;
			if (!CheckSpecialSymbol("}"))
			{
				Error("Очаквам специален символ '}'");
				return false;
			}
			return true;
		}

		//[4]  Declaration = VarDef | FuncDef.
		public bool IsDeclaration()
		{
			if (!IsVarDef() || !IsFuncDef()) return false;
			return true;
		}

		//[5]  VarDef = TypeIdent Ident.
		public bool IsVarDef()
		{
			if (!CheckIdent()) Error("Очаквам идентификатор 1");
			return true;
		}

		//[6]  FuncDef = TypeIdent Ident '(' TypeIdent Ident ')' CompoundSt.
		public bool IsFuncDef()
		{
			if (!CheckIdent()) Error("Очаквам идентификатор 2");
			if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
			if (!CheckIdent()) Error("Очаквам идентификатор 3");
			if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");
			if (!IsCompoundSt()) return false;
			return true;
		}

		//[7]  IfSt = 'if' '(' Expression ')' Statement ['else' Statement].
		public bool IsIfSt()
		{
			if (!CheckKeyword("if")) Error("Очаквам ключова дума 'if'");
			if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
			if (!IsExpression()) Error("Очаквам израз");
			if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");

			if (IsStatement())
			{
				if (!CheckKeyword("else")) Error("Очаквам ключова дума 'else'");
				if (!IsStatement()) Error("Очаквам statement");
			}

			return true;
		}

		//[8]  WhileSt = 'while' '(' Expression ')' Statement. 
		public bool IsWhileSt()
		{
			if (!CheckKeyword("while")) Error("Очаквам ключова дума 'while'");
			if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
			if (!IsExpression()) Error("Очаквам израз");
			if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");
			if (!IsStatement()) Error("Очаквам statement");
			return true;
		}

		//[9]  StopSt = 'break' ';' | 'continue' ';' | 'return' [Expression] ';'
		public bool IsStopSt()
		{
			if (CheckKeyword("return"))
			{
				IsExpression();
				if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

				// Emit
				emit.AddReturn();

			}
			else if (CheckKeyword("break"))
			{
				if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

				// Emit
				emit.AddBranch((Label)breakStack.Peek());

			}
			else if (CheckKeyword("continue"))
			{
				if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

				// Emit
				emit.AddBranch((Label)continueStack.Peek());
			}
			return true;
		}

		//[10] Expression =  AdditiveExpr [('<' | '<=' | '==' | '!=' | '>=' | '>') AdditiveExpr].
		public bool IsExpression()
		{
			if (!IsAdditiveExpr()) return false;
			SpecialSymbolToken opToken = token as SpecialSymbolToken;
			if (CheckSpecialSymbol("<") || CheckSpecialSymbol("<=") || CheckSpecialSymbol("==") || CheckSpecialSymbol("!=") || CheckSpecialSymbol(">=") || CheckSpecialSymbol(">"))
			{

				if (!IsAdditiveExpr()) Error("Очаквам адитивен израз");

				//Emit
				emit.AddConditionOp(opToken.value);

			}
			return true;
		}

		//[11] AdditiveExpr = ['+' | '-'] MultiplicativeExpr {('+' | '-' | '|' | '||') MultiplicativeExpr}.
		public bool IsAdditiveExpr()
		{
			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				if (!IsMultiplicativeExpr()) Error("Очаквам мултипликативен израз");
			}

			while (CheckSpecialSymbol("+") || CheckSpecialSymbol("-") || CheckSpecialSymbol("|") || CheckSpecialSymbol("||"))
			{
				if (!IsMultiplicativeExpr()) Error("Очаквам мултипликативен израз");
			}

			return true;
		}

		//[12] MultiplicativeExpr = SimpleExpr {('*' | '/' | '%' | '&' | '&&') SimpleExpr}.
		public bool IsMultiplicativeExpr()
		{
			if (!IsSimpleExpr()) return false;

			SpecialSymbolToken opToken = token as SpecialSymbolToken;
			while (CheckSpecialSymbol("*") || CheckSpecialSymbol("/") || CheckSpecialSymbol("%") || CheckSpecialSymbol("&") || CheckSpecialSymbol("&&"))
			{
				if (!IsSimpleExpr()) Error("Очаквам прост израз");

				//Emit
				emit.AddMultiplicativeOp(opToken.value);

				opToken = token as SpecialSymbolToken;
			}

			return true;
		}

		//[13] SimpleExpr = ('++' | '--' | '-' | '~' | '!') PrimaryExpr | PrimaryExpr ['++' | '--'].
		public enum IncDecOps { None, PreInc, PreDec, PostInc, PostDec }
		public bool IsSimpleExpr()
		{
			if (CheckSpecialSymbol("++") || CheckSpecialSymbol("--") || CheckSpecialSymbol("-") || CheckSpecialSymbol("~") || CheckSpecialSymbol("!"))
			{
				if (IsPrimaryExpr()) IsPrimaryExpr();
			}

			if (IsPrimaryExpr())
			{
				CheckSpecialSymbol("++");
				CheckSpecialSymbol("--");
			}

			return true;
		}
		/*[14] PrimaryExpr = Constant | Variable | VarIdent [('='|'+='|'-='|'*='|'/='|'%=') Expression] |
			'*' VarIdent | '&' VarIdent | FuncIdent '(' [Expression] ')' | '(' Expression ')'.*/
		public bool IsPrimaryExpr()
		{
			if (IsConstant(out Type type))
			{
				if (!IsConstant(out type)) Error("Очаквам константа");
				return true;
			}

			if (IsVariable())
			{
				if (!IsVariable()) Error("Очаквам променлива");
				return true;
			}

			if (CheckSpecialSymbol("=") || CheckSpecialSymbol("+=") || CheckSpecialSymbol("-=") || CheckSpecialSymbol("*=") || CheckSpecialSymbol("/=") || CheckSpecialSymbol("%="))
			{
				if (!IsExpression()) Error("Очаквам израз");
				return true;
			}

			if (CheckSpecialSymbol("*") && IsVariable())
			{
				if (!CheckSpecialSymbol("*") && !IsVariable()) Error("Очаквам специален символ '*' с променлива");
				return true;
			}

			if (CheckSpecialSymbol("&") && IsVariable())
			{
				if (!CheckSpecialSymbol("&") && !IsVariable()) Error("Очаквам специален символ '&' с променлива");
				return true;
			}

			if (IsFuncIdent())
			{
				if (!IsFuncIdent()) Error("Очаквам функция");
				return true;
			}

			if (IsExpression())
			{
				if (!IsExpression()) Error("Очаквам израз");
				return true;
			}

			return false;
		}

		public bool IsFuncIdent()
		{
			if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
			if (IsExpression())
			{
				if (!IsExpression()) Error("Очаквам израз");
			}
			if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");
			return true;
		}

		//[15] Constant = ['+'|'-'] (Number | ConstIdent) | String.
		public bool IsConstant(out Type type)
		{
			Token literal = token;

			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				if (CheckNumber())
				{
					type = typeof(System.Int32);
					emit.AddGetNumber(((NumberToken)literal).value);
				}
				//	else if (!IsConstIdent()) Error("Очаквам идентификатор за константа");
			}

			if (CheckNumber())
			{
				type = typeof(System.Int32);
				emit.AddGetNumber(((NumberToken)literal).value);
			}
			//else if (!IsConstIdent()) Error("Очаквам идентификатор за константа");

			if (CheckString())
			{
				type = typeof(System.String);
				emit.AddGetString(((StringToken)literal).value);
			}
			else
			{
				type = null;
				return false;
			}

			return true;
		}
		//[16] Variable = ['+'|'-'] VarIdent.
		public bool IsVariable()
		{
			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				if (!CheckIdent()) Error("Очаквам идентификатор на променлива");
			}

			if (!CheckIdent()) Error("Очаквам идентификатор на променлива");

			return true;
		}

		public bool AssignableTypes(Type typeAssignTo, Type typeAssignFrom)
		{
			//return typeAssignTo==typeAssignFrom;
			return typeAssignTo.IsAssignableFrom(typeAssignFrom);
		}
		
		public class LocationInfo
		{
			public TableSymbol id;
			public bool isArray;
		}
	}
}
