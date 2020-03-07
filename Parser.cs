using System;
using System.Collections.Generic;
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
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1, -1, "int"), typeof(System.Int32)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1, -1, "bool"), typeof(System.Boolean)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1, -1, "char"), typeof(System.Char)));
			symbolTable.AddToUniverse(new PrimitiveTypeSymbol(new IdentToken(-1, -1, "string"), typeof(System.String)));
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
			bool result = (token is KeywordToken) && ((KeywordToken)token).value == keyword;
			if (result) ReadNextToken();
			
			return result;
		}

		public bool CheckSpecialSymbol(string symbol)
		{
			bool result = (token is SpecialSymbolToken) && ((SpecialSymbolToken)token).value == symbol;
			if (result) ReadNextToken();
			return result;
		}

		public bool CheckIdent()
		{
			bool result = (token is IdentToken);
			if (result) ReadNextToken();
			return result;
		}

		public bool CheckEOF()
		{
			bool result = (token is EOFToken);
			return result;
		}

		public bool isBuildInFunction(string bif)
		{
			bool result = (token is IdentToken) && ((IdentToken)token).value == bif;
			if (result) ReadNextToken();
			return result;
		}

		public bool CheckNumber()
		{
			bool result = (token is NumberToken);
			if (result) ReadNextToken();
			return result;
		}

		public bool CheckString()
		{
			bool result = (token is StringToken);
			if (result) ReadNextToken();
			return result;
		}

		void SkipUntilSemiColon()
		{
			Token Tok;
			do
			{
				Tok = scanner.Next();
			} while (!((Tok is EOFToken) ||
						 (Tok is SpecialSymbolToken) && ((Tok as SpecialSymbolToken).value == ";")));
		}

		public void Error(string message)
		{
			diag.Warning(token.line, token.column, message);
			SkipUntilSemiColon();
		}

		public void Error(string message, Token token)
		{
			diag.Warning(token.line, token.column, message);
			SkipUntilSemiColon();
		}

		public void Error(string message, Token token, params object[] par)
		{
			diag.Warning(token.line, token.column, string.Format(message, par));
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
			while (IsStatement());
			
			//Console.WriteLine("");

			return diag.GetErrorCount() == 0;
		}

		//[2]  Statement = CompoundSt | IfSt | WhileSt | StopSt | [Expression] ';'.
		public bool IsStatement()
		{
			Console.WriteLine("Debug: " + token);

			/*if(token.GetType() == null || token.GetType() == typeof(EOFToken))
			{
				return false;
			}*/

			Type type;

			/*if (CheckEOF())
			{
				return false;
			}
			else*/ if (IsCompoundSt())
			{
				ReadNextToken();
				return true;
			}
			else if (IsIfSt())
			{
				ReadNextToken();
				return true;
			}
			else if (IsWhileSt())
			{
				ReadNextToken();
				return true;
			}
			else if (IsStopSt())
			{
				ReadNextToken();
				return true;
			}
			else if (IsExpression(out type))
			{
				ReadNextToken();
				return true;
			}

			if (CheckSpecialSymbol(";"))
			{
				ReadNextToken();
				return true;
			}

			return false;
		}

		//[3]  CompoundSt = '{' {Declaration} {Statement} '}'
		public bool IsCompoundSt()
		{
			Warning("Влиза в IsCompoundSt");
			if (!CheckSpecialSymbol("{")) return false;
			
			while (IsDeclaration());
			
			while (IsStatement());
			
			if (!CheckSpecialSymbol("}")) Error("Очаквам специален символ '}'");
			
			return true;
		}

		//[4]  Declaration = VarDef | FuncDef.
		public bool IsDeclaration()
		{
			Warning("Влиза IsDeclaration");

			IsFuncDefOrVarDef();

			return true;
		}

		//[5]  VarDef = TypeIdent Ident. + [6]  FuncDef = TypeIdent Ident '(' TypeIdent Ident ')' CompoundSt.
		public bool IsFuncDefOrVarDef()
		{
			Warning("Влиза в IsFuncDefOrVarDef");
			/*if (isBuildInFunc() && CheckSpecialSymbol("(") )
			{
				if (IsVariable())
				{
					if (CheckSpecialSymbol(")") && CheckSpecialSymbol(";")) return true;
				}

				if(CheckSpecialSymbol(")") && CheckSpecialSymbol(";")) return true;
			}
			*/
			Type type;
			IdentToken name;

			if (!IsType(out type)) return false;
			name = token as IdentToken;
			if (!CheckIdent()) Error("Очаквам идентификатор");
			//if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

			// Семантична грешка - редекларирана ли е локалната променлива повторно?
			if (symbolTable.ExistCurrentScopeSymbol(name.value)) Error("Локалната променлива {0} е редекларирана", name, name.value);
			// Emit
			symbolTable.AddLocalVar(name, emit.AddLocalVar(name.value, type));
		
			if (CheckSpecialSymbol("("))
			{
				if (!IsType(out type)) return false;
				name = token as IdentToken;
				if (!CheckIdent()) Error("Очаквам идентификатор");
				//if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

				// Семантична грешка - редекларирана ли е локалната променлива повторно?
				if (symbolTable.ExistCurrentScopeSymbol(name.value)) Error("Локалната променлива {0} е редекларирана", name, name.value);
				// Emit
				symbolTable.AddLocalVar(name, emit.AddLocalVar(name.value, type));
				if (!IsCompoundSt()) return false;
			}

			return true;
		}

		//[7]  IfSt = 'if' '(' Expression ')' Statement ['else' Statement].
		public bool IsIfSt()
		{
			Type type;
			Warning("Влиза в IsIfSt");
			if (CheckKeyword("if"))
			{
				if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
				if (!IsExpression(out type)) Error(" 55 Очаквам Expression");
				if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");
			}
			else return false;
			//emit
			Label labelElse = emit.GetLabel();
			emit.AddCondBranch(labelElse);

			if (!IsStatement()) Error("Очаквам Statement");

			if (CheckKeyword("else"))
			{
				// Emit
				Label labelEnd = emit.GetLabel();
				emit.AddBranch(labelEnd);
				emit.MarkLabel(labelElse);

				if (!IsStatement()) Error("Очаквам Statement");
				// Emit
				emit.MarkLabel(labelEnd);
			}else
			{
				// Emit
				emit.MarkLabel(labelElse);
			}
			
			return true;
		}

		//[8]  WhileSt = 'while' '(' Expression ')' Statement. 
		public bool IsWhileSt()
		{
			Type type;
			Warning("Влиза в IsWhileSt");

			if (CheckKeyword("while"))
			{
				// Emit
				Label labelContinue = emit.GetLabel();
				Label labelBreak = emit.GetLabel();
				breakStack.Push(labelBreak);
				continueStack.Push(labelContinue);

				emit.MarkLabel(labelContinue);

				if (!CheckSpecialSymbol("(")) Error("Очаквам специален символ '('");
				if (!IsExpression(out type)) Error("12 Очаквам израз");
				if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");

				// Emit
				emit.AddCondBranch(labelBreak);

				if (!IsStatement()) Error("Очаквам statement");

				// Emit
				emit.AddBranch(labelContinue);
				emit.MarkLabel(labelBreak);

				breakStack.Pop();
				continueStack.Pop();
			}
			else return false;
			
			return true;
		}

		//[9]  StopSt = 'break' ';' | 'continue' ';' | 'return' [Expression] ';'
		public bool IsStopSt()
		{
			Type type;
			Warning("Влиза в IsStopSt");
			if (CheckKeyword("break"))
			{
				if (!CheckSpecialSymbol(";")) Error("Очаквам специален символ ';'");

				// Emit
				emit.AddBranch((Label)breakStack.Peek());

			}
			else if (CheckKeyword("continue"))
			{
				if (!CheckSpecialSymbol(";")) Warning("Очаквам специален символ ';'");

				// Emit
				emit.AddBranch((Label)continueStack.Peek());
			}
			else if (CheckKeyword("return"))
			{
				IsExpression(out type);
				if (!CheckSpecialSymbol(";")) Warning("Очаквам специален символ ';'");

				// Emit
				emit.AddReturn();

			}
			else
			{
				return false;
			}

			return true;
		}

		//[10] Expression =  AdditiveExpr [('<' | '<=' | '==' | '!=' | '>=' | '>') AdditiveExpr].
		public bool IsExpression(out Type type)
		{
			Warning("Влиза в IsExpression");
			if (!IsAdditiveExpr(out type)) return false;
			SpecialSymbolToken opToken = token as SpecialSymbolToken;
			if (CheckSpecialSymbol("<") || CheckSpecialSymbol("<=") || CheckSpecialSymbol("==") || CheckSpecialSymbol("!=") || CheckSpecialSymbol(">=") || CheckSpecialSymbol(">"))
			{
				Type type1;
				if (!IsAdditiveExpr(out type1)) Error("Очаквам адитивен израз");
				if (type != type1) Error("Несъвместими типове за сравнение");
				//Emit
				emit.AddConditionOp(opToken.value);

				type = typeof(System.Boolean);
			}

			return true;
		}

		//[11] AdditiveExpr = ['+' | '-'] MultiplicativeExpr {('+' | '-' | '|' | '||') MultiplicativeExpr}.
		public bool IsAdditiveExpr(out Type type)
		{
			Warning("Влиза в IsAdditiveExpr");

			SpecialSymbolToken opToken = token as SpecialSymbolToken;
			bool unaryMinus = false;
			bool unaryOp = false;

			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				//unaryMinus = ((SpecialSymbolToken)token).value == "-";
				unaryOp = true;
			}

			if (!IsMultiplicativeExpr(out type)){
				if (unaryOp){ 
					Error("Очаквам мултипликативен израз"); 
				}else return false;
			}

			// Emit
			if (unaryMinus)
			{
				emit.AddUnaryOp("-");
			}
			
			opToken = token as SpecialSymbolToken;

			while (CheckSpecialSymbol("+") || CheckSpecialSymbol("-") || CheckSpecialSymbol("|") || CheckSpecialSymbol("||")){
				Type type1;
				if (!IsMultiplicativeExpr(out type1))Error("Очаквам мултипликативен израз");
				
				// Types check
				if (opToken.value == "||")
				{
					if (type == typeof(System.Boolean) && type1 == typeof(System.Boolean))
					{
						;
					}
					else
					{
						Error("Несъвместими типове", opToken);
					}
				}
				else
				{
					if (type == typeof(System.Int32) && type1 == typeof(System.Int32))
					{
						;
					}
					else if (type == typeof(System.Int32) && type1 == typeof(System.Double))
					{
						type = typeof(System.Double);
						Warning("Трябва да използвате явно конвертиране на първия аргумент до double", opToken);
					}
					else if (type == typeof(System.Double) && type1 == typeof(System.Int32))
					{
						type = typeof(System.Double);
						Warning("Трябва да използвате явно конвертиране на втория аргумент до double", opToken);
					}
					else if (type == typeof(System.Double) && type1 == typeof(System.Double))
					{
						;
					}
					else if (type == typeof(System.String) || type1 == typeof(System.String))
					{
						type = typeof(System.String);
					}
					else
					{
						Error("Несъвместими типове");
					}
				}

				//Emit
				if (opToken.value == "+" && type == typeof(System.String))
				{
					emit.AddConcatinationOp();
				}
				else
				{
					emit.AddAdditiveOp(opToken.value);
				}

				opToken = token as SpecialSymbolToken;
			}

			return true;
		}

		//[12] MultiplicativeExpr = SimpleExpr {('*' | '/' | '%' | '&' | '&&') SimpleExpr}.
		public bool IsMultiplicativeExpr(out Type type)
		{
			Warning("Влиза в isMultiplicativeExpr");

			
			if (!IsSimpleExpr(out type)) return false;

			SpecialSymbolToken opToken = token as SpecialSymbolToken;
			
			while (CheckSpecialSymbol("*") || CheckSpecialSymbol("/") || CheckSpecialSymbol("%") || CheckSpecialSymbol("&") || CheckSpecialSymbol("&&"))
			{
				Type type1;
				
				if (!IsSimpleExpr(out type1)) Error("Очаквам прост израз");
				
				// Types check
				if (opToken.value == "&&")
				{
					if (type == typeof(System.Boolean) && type1 == typeof(System.Boolean))
					{
						type = typeof(System.Boolean);
					}
					else
					{
						Error("Несъвместими типове");
					}
				}
				else
				{
					if (type == typeof(System.Int32) && type1 == typeof(System.Int32))
					{
						type = typeof(System.Int32);
					}
					else if (type == typeof(System.Int32) && type1 == typeof(System.Double))
					{
						type = typeof(System.Double);
						Warning("Трябва да използвате явно конвертиране на първия аргумент до double", opToken);
					}
					else if (type == typeof(System.Double) && type1 == typeof(System.Int32))
					{
						type = typeof(System.Double);
						Warning("Трябва да използвате явно конвертиране на втория аргумент до double", opToken);
					}
					else if (type == typeof(System.Double) && type1 == typeof(System.Double))
					{
						type = typeof(System.Double);
					}
					else
					{
						Error("Несъвместими типове NULL");
					}
				}
				
				//Emit
				emit.AddMultiplicativeOp(opToken.value);

				opToken = token as SpecialSymbolToken;
			}

			return true;
		}

		//[13] SimpleExpr = ('++' | '--' | '-' | '~' | '!') PrimaryExpr | PrimaryExpr ['++' | '--'].
		public enum IncDecOps { None, PreInc, PreDec, PostInc, PostDec }
		public bool IsSimpleExpr(out Type type)
		{
			Warning("Влиза в IsSimpleExpr");

			SpecialSymbolToken opToken;

			IncDecOps incDecOp = IncDecOps.None;

			opToken = token as SpecialSymbolToken;
			if (CheckSpecialSymbol("++"))
			{
				incDecOp = IncDecOps.PreInc;
			}
			else if (CheckSpecialSymbol("--")) incDecOp = IncDecOps.PreDec;

			if(incDecOp == IncDecOps.PreInc || incDecOp == IncDecOps.PreDec)
			{
				if (!IsPrimaryExpr(out type)) Error("Очаквам primaryExpr");
			}

			if (incDecOp == IncDecOps.None)
			{
				opToken = token as SpecialSymbolToken;
				if (CheckSpecialSymbol("++")) incDecOp = IncDecOps.PostInc;
				else if (CheckSpecialSymbol("--")) incDecOp = IncDecOps.PostDec;
			}

			if (incDecOp == IncDecOps.PostInc || incDecOp == IncDecOps.PostDec) {
				if (!IsPrimaryExpr(out type)) Error("Очаквам прост израз");
			}

			if (CheckSpecialSymbol("-") || CheckSpecialSymbol("~") || CheckSpecialSymbol("!")) {
				if (!IsPrimaryExpr(out type)) Error("Очаквам прост израз");

				// Emit
				emit.AddUnaryOp(opToken.value);
			} else {
				type = null;
				//return false;
			}
					
			return true;
		}

		/*[14] PrimaryExpr = Constant | Variable | VarIdent [('='|'+='|'-='|'*='|'/='|'%=') Expression] |
			'*' VarIdent | '&' VarIdent | FuncIdent '(' [Expression] ')' | '(' Expression ')'.*/
		public bool IsPrimaryExpr(out Type type)
		{
			type = typeof(System.Int32); ;
			IdentToken name;
			NumberToken constant;

			Warning("Влиза в IsPrimaryExpr");

			if (IsConstant())
			{
				constant = token as NumberToken;
				name = token as IdentToken;
				Console.WriteLine("Debug: Constant");
				//symbolTable.Add(emit.AddGetNumber(constant.value));
				symbolTable.AddLocalVar(name, emit.AddLocalVar(""+constant.value, type));
				Console.WriteLine("Debug: Constant");
				return true;
			}

			if (IsVariable())
			{
				name = token as IdentToken;
				Console.WriteLine("Debug: Constant");
				symbolTable.AddLocalVar(name, emit.AddLocalVar(name.value, type));

				Console.WriteLine("Debug: Variable");
				return true;
			}

			//VarIdent [('='|'+='|'-='|'*='|'/='|'%=') Expression]
			if (CheckIdent())
			{
				if (CheckSpecialSymbol("=") || CheckSpecialSymbol("+=") || CheckSpecialSymbol("-=") || CheckSpecialSymbol("*=") || CheckSpecialSymbol("/=") || CheckSpecialSymbol("%="))
				{
					if (!IsExpression(out type))
					{
						Warning("isPrimaryExpr() Очаквам израз");
						return false;
					}
				}
			}
			
			//'*' VarIdent 
			if (CheckSpecialSymbol("*"))
			{
				if (!CheckIdent()) Error("Очаквам идентификатор");// return false;
			}

			//'&' VarIdent
			if (CheckSpecialSymbol("&"))
			{
				if (!CheckIdent()) Error("Очаквам идентификатор");//return false;
			}

			//FuncIdent '(' [Expression] ')'
			//funcIdent e imaeto na funckiqta
			
			/*if (isBuildInFunc())
			{
				Console.WriteLine("Debug: " + token);
				return true;
			}*/

			// '(' Expression ')'
			if (CheckSpecialSymbol("(")) {
				if (!IsExpression(out type)) Error("Очаквам expression");
				if (!CheckSpecialSymbol(")")) Error("Очаквам специален символ ')'");
			}
			

			return true;
		}
		/*
		public bool IsFuncIdent()
		{
			if (!CheckSpecialSymbol("("))
			{
				Warning("Очаквам специален символ '('");
				return false;
			}

			IsExpression();

			if (!CheckSpecialSymbol(")"))
			{
				Warning("Очаквам специален символ ')'");
				return false;
			}

			return true;
		}
		*/
		//[15] Constant = ['+'|'-'] (Number | ConstIdent) | String.
		public bool IsConstant()
		{

			bool unaryOp = false;

			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				unaryOp = true;
			}

			if (!CheckNumber() || !CheckKeyword("const"))
			{
				if (unaryOp) return false;
			}//else if (!CheckString()) return false;


			return true;
		}
		//[16] Variable = ['+'|'-'] VarIdent.
		public bool IsVariable()
		{
			bool unaryOp = false;

			if (CheckSpecialSymbol("+") || CheckSpecialSymbol("-"))
			{
				unaryOp = true;
			}

			if (!CheckIdent())
			{
				if (unaryOp) return false;
			}

			return true;
		}

		public bool IsType(out Type type)
		{
			if (CheckKeyword("int")) {
				type = typeof(System.Int32);
				return true;
			}
			if (CheckKeyword("bool")) {
				type = typeof(System.Boolean);
				return true;
			}
			if (CheckKeyword("char")) {
				type = typeof(System.Char);
				return true;
			}
			if (CheckKeyword("string")) {
			type = typeof(System.String);
				return true;
			}
			IdentToken typeIdent = token as IdentToken;
			if (typeIdent != null)
			{
				TypeSymbol ts = symbolTable.GetSymbol(typeIdent.value) as TypeSymbol;
				if (ts != null)
					type = ts.type;
				else
					type = symbolTable.ResolveExternalType(typeIdent.value);

				if (type != null)
				{
					ReadNextToken();
					return true;
				}
			}
			
			type = null;
			return false;
		}

		public bool isBuildInFunc()
		{
			if (isBuildInFunction("scanf"))
			{
				ReadNextToken();
				return true;
			}

			if (isBuildInFunction("printf"))
			{
				ReadNextToken();
				return true;
			}

			if (isBuildInFunction("abs"))
			{
				ReadNextToken();
				return true;
			}

			if (isBuildInFunction("sqr"))
			{
				ReadNextToken();
				return true;
			}

			if (isBuildInFunction("odd"))
			{
				ReadNextToken();
				return true;
			}

			if (isBuildInFunction("ord"))
			{
				ReadNextToken();
				return true;
			}

			return false;
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
