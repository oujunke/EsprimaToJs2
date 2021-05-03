using Esprima;
using Esprima.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
namespace EsprimaToJs
{
    public class NodeConv
    {
        public Stream WriteStream;
        public int Spacing;
        /// <summary>
        /// 上次获取位置数据
        /// </summary>
        public int LastGetSpacing;
        public const string SpacingStr = "\t";
        public int SpacingStrByteLength = Encoding.UTF8.GetBytes(SpacingStr).Length;
        /// <summary>
        /// 换行是否存在间隔(避免连续换行)
        /// </summary>
        private bool _newLine = false;
        /// <summary>
        /// 新分号
        /// </summary>
        private bool _newBranch = false;
        public NodeConv(Stream stream)
        {
            WriteStream = stream;
        }
        /// <summary>
        /// 首尾添加字符串
        /// </summary>
        private void BeginEndAddBody(bool isAdd, Action act, string begin, string end)
        {
            if (isAdd)
            {
                if (!string.IsNullOrWhiteSpace(begin))
                {
                    WriteString(begin);
                }
                act();
                if (!string.IsNullOrWhiteSpace(end))
                {
                    WriteString(end);
                }
            }
            else
            {
                act();
            }
        }
        /// <summary>
        /// 首尾添加执行方法
        /// </summary>
        private void BeginEndAddBody(bool isAdd, Action act, Action begin, Action end)
        {
            if (isAdd)
            {
                begin?.Invoke();
                act();
                end?.Invoke();
            }
            else
            {
                act();
            }
        }
        /// <summary>
        /// 写入NodeList
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="options"></param>
        public void WriteNode(IEnumerable<Node> nodes, Options options = Options.None)
        {
            if (nodes == null)
            {
                return;
            }
            foreach (var item in nodes)
            {
                WriteNode(item, options);
            }
        }
        /// <summary>
        /// 写入NodeList
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="options"></param>
        public void WriteNode(IEnumerable<Node> nodes, Action<Node, Options> action, Options options = Options.None)
        {
            if (nodes == null)
            {
                return;
            }
            foreach (var item in nodes)
            {
                action(item, options);
            }
        }
        /// <summary>
        /// 写入Node
        /// </summary>
        /// <param name="node"></param>
        public void WriteNode(Node node, Options options = Options.None)
        {
            if (node == null)
            {
                return;
            }
            switch (node.Type)
            {
                //声明 (var|let|const)
                case Nodes.VariableDeclaration:
                    var variableDeclaration = node as VariableDeclaration;
                    {
                        string head = variableDeclaration.Kind.ToString().ToLower();
                        var num = 0;
                        var isNewLine = !options.HasFlag(Options.VariableDeclarationNotNewLine);
                        WriteNode(node.ChildNodes, (i, o) =>
                        {
                            BeginEndAddBody(isNewLine, () =>
                            {
                                if (options.HasFlag(Options.VariableDeclarationComma) && num++ > 0)
                                {
                                    WriteString(",");
                                }
                                else
                                {
                                    WriteString($"{head} ");
                                }
                                WriteNode(i, o);
                            }, NewLine, NewLine);
                        }, options);
                    }
                    break;
                //赋值 xxx=xxx
                case Nodes.VariableDeclarator:
                    var variableDeclarator = (VariableDeclarator)node;
                    BeginEndAddBody(options.HasFlag(Options.VariableDeclaratorBrackets), () =>
                    {
                        WriteNode(variableDeclarator.Id);
                        if (variableDeclarator.Init != null)
                        {
                            WriteString("=");
                            WriteNode(variableDeclarator.Init);
                        }
                        if (!options.HasFlag(Options.VariableDeclaratorNotBranch))
                        {
                            NewBranch();
                        }
                    }, "(", ")");
                    break;
                //变量名
                case Nodes.Identifier:
                    WriteString(((Identifier)node).Name);
                    break;
                //构造对象
                case Nodes.ObjectExpression:
                    var objectExpression = node as ObjectExpression;
                    if (objectExpression.Properties.Count > 0)
                    {
                        WriteString("{");
                        Spacing++;
                        NewLine();
                        for (int i = 0; i < objectExpression.Properties.Count; i++)
                        {
                            if (i > 0)
                            {
                                WriteString(",");
                                NewLine();
                            }
                            WriteNode(objectExpression.Properties[i]);
                        }
                        Spacing--;
                        NewLine();
                        WriteString("}");
                    }
                    else
                    {
                        WriteString("{}");
                    }
                    break;
                //方法
                case Nodes.FunctionExpression:
                    var functionExpression = node as FunctionExpression;
                    BeginEndAddBody(options.HasFlag(Options.FunctionExpressionBrackets), () =>
                    {
                        WriteString("function(");
                        WriteParams(functionExpression.Params);
                        WriteString("){");
                        Spacing++;
                        NewLine();
                        WriteNode(functionExpression.Body, Options.BlockStatementLine);
                        Spacing--;
                        NewLineOrBackspace();
                        WriteString("}");
                    }, "(", ")");
                    break;
                //属性
                case Nodes.Property:
                    var property = node as Property;
                    WriteNode(property.Key);
                    WriteString(":");
                    WriteNode(property.Value);
                    break;
                //值
                case Nodes.Literal:
                    var literal = node as Literal;
                    WriteString(literal.Raw);
                    break;
                //块语句，即用括号括起来的一系列语句。
                case Nodes.BlockStatement:
                    NewLine();
                    foreach (var item in node.ChildNodes)
                    {
                        WriteNode(item);
                        if (options.HasFlag(Options.BlockStatementLine))
                        {
                            NewLine();
                        }
                    }
                    break;
                //return 语句
                case Nodes.ReturnStatement:
                    NewLine();
                    WriteString("return ");
                    WriteNode(node.ChildNodes);
                    NewBranch();
                    break;
                //语句块
                case Nodes.ExpressionStatement:
                    foreach (var item in node.ChildNodes)
                    {
                        WriteNode(item);
                        NewBranch();
                        NewLine();
                    }
                    break;
                //赋值
                case Nodes.AssignmentExpression:
                    var assignmentExpression = node as AssignmentExpression;
                    BeginEndAddBody(options.HasFlag(Options.VariableDeclaratorBrackets), () =>
                    {
                        //NewLine();
                        WriteNode(assignmentExpression.Left);
                        WriteOperator(assignmentExpression.Operator);
                        WriteNode(assignmentExpression.Right);
                    }, "(", ")");
                    //NewBranch();
                    break;
                //读取对象属性
                case Nodes.MemberExpression:
                    var memberExpression = node as MemberExpression;
                    WriteNode(memberExpression.Object, options);
                    if (memberExpression.Computed)
                    {
                        WriteString("[");
                        WriteNode(memberExpression.Property, options);
                        WriteString("]");
                    }
                    else
                    {
                        WriteString(".");
                        WriteNode(memberExpression.Property, options);
                    }
                    break;
                //创建方法
                case Nodes.FunctionDeclaration:
                    var functionDeclaration = node as FunctionDeclaration;
                    NewLine();
                    WriteString("function ");
                    WriteNode(functionDeclaration.Id);
                    WriteString("(");
                    WriteParams(functionDeclaration.Params);
                    WriteString("){");
                    Spacing++;
                    NewLine();
                    WriteNode(functionDeclaration.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    NewLine();
                    break;
                //方法调用
                case Nodes.CallExpression:
                    var callExpression = node as CallExpression;
                    WriteNode(callExpression.Callee, Options.FunctionExpressionBrackets);
                    WriteString("(");
                    WriteParams(callExpression.Arguments);
                    WriteString(")");
                    break;
                //if 语句
                case Nodes.IfStatement:
                    var ifStatement = node as IfStatement;
                    if (!options.HasFlag(Options.IfStatementNoLine))
                    {
                        NewLine();
                    }
                    WriteString("if(");
                    WriteNode(ifStatement.Test, Options.VariableDeclaratorBrackets);
                    WriteString("){");
                    Spacing++;
                    NewLine();
                    WriteNode(ifStatement.Consequent);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    NewLine();
                    if (ifStatement.Alternate != null)
                    {
                        if (ifStatement.Alternate.Type == Nodes.IfStatement)
                        {
                            WriteString("else ");
                            WriteNode(ifStatement.Alternate, options | Options.IfStatementNoLine);
                        }
                        else
                        {
                            WriteString("else{");
                            Spacing++;
                            NewLine();
                            WriteNode(ifStatement.Alternate, options);
                            Spacing--;
                            NewLineOrBackspace();
                            WriteString("}");
                        }
                    }

                    break;
                //二进制运算符表达式。
                case Nodes.BinaryExpression:
                //逻辑运算符表达式
                case Nodes.LogicalExpression:
                    var binaryExpression = node as BinaryExpression;
                    WriteString("(");
                    WriteNode(binaryExpression.Left, options | Options.VariableDeclaratorBrackets| Options.ConditionalExpressionBrackets);
                    WriteOperator(binaryExpression.Operator);
                    WriteNode(binaryExpression.Right, options | Options.VariableDeclaratorBrackets | Options.ConditionalExpressionBrackets);
                    WriteString(")");
                    break;
                case Nodes.UnaryExpression:
                    var unaryExpression = node as UnaryExpression;
                    //var unaryFieldInfo = unaryExpression.Operator.GetType().GetField(unaryExpression.Operator.ToString());
                    //WriteString($" {unaryFieldInfo.CustomAttributes.ElementAt(0).NamedArguments[0].TypedValue.Value} ");
                    WriteString("(");
                    WriteOperator(unaryExpression.Operator);
                    WriteNode(unaryExpression.Argument, options | Options.VariableDeclaratorBrackets);
                    WriteString(")");
                    break;
                //逗号表达式
                case Nodes.SequenceExpression:
                    var sequenceExpression = node as SequenceExpression;
                    WriteString("(");
                    WriteParams(sequenceExpression.Expressions, options.HasFlag(Options.SequenceExpressionNotLine) ? options : Options.NewLine | options);
                    WriteString(")");
                    break;
                //for in
                case Nodes.ForInStatement:
                    ForInStatement forInStatement = node as ForInStatement;
                    WriteString("for(");
                    WriteNode(forInStatement.Left, Options.VariableDeclarationNotNewLine | Options.VariableDeclaratorNotBranch);
                    WriteString(" in ");
                    WriteNode(forInStatement.Right);
                    WriteString("){");
                    Spacing++;
                    NewLine();
                    WriteNode(forInStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                //一个条件表达式，即三元表达式 ? :
                case Nodes.ConditionalExpression:
                    var conditionalExpression = node as ConditionalExpression;
                    BeginEndAddBody(options.HasFlag(Options.ConditionalExpressionBrackets), () =>
                    {
                        WriteNode(conditionalExpression.Test, options | Options.VariableDeclaratorBrackets);
                        WriteString("?");
                        WriteNode(conditionalExpression.Consequent);
                        WriteString(":");
                        WriteNode(conditionalExpression.Alternate);
                    }, "(", ")");

                    break;
                //for
                case Nodes.ForStatement:
                    var forStatement = node as ForStatement;
                    NewLine();
                    WriteString("for(");
                    WriteNode(forStatement.Init, Options.VariableDeclarationNotNewLine | Options.VariableDeclaratorNotBranch | Options.VariableDeclarationComma);
                    WriteString(" ; ");
                    WriteNode(forStatement.Test);
                    WriteString(" ; ");
                    WriteNode(forStatement.Update);
                    WriteString(" ){");
                    Spacing++;
                    NewLine();
                    WriteNode(forStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                // var array=[]
                case Nodes.ArrayExpression:
                    var arrayExpression = node as ArrayExpression;
                    WriteString("[");
                    WriteParams(arrayExpression.Elements);
                    WriteString("]");
                    break;
                //自增
                case Nodes.UpdateExpression:
                    var updateExpression = node as UpdateExpression;
                    WriteNode(updateExpression.Argument);
                    WriteOperator(updateExpression.Operator);
                    break;
                //创建对象
                case Nodes.NewExpression:
                    var newExpression = node as NewExpression;
                    WriteString("new ");
                    WriteNode(newExpression.Callee);
                    WriteString("(");
                    WriteParams(newExpression.Arguments);
                    WriteString(")");
                    break;
                //this语句
                case Nodes.ThisExpression:
                    WriteString("this");
                    break;
                //try语句
                case Nodes.TryStatement:
                    var tryStatement = node as TryStatement;
                    NewLine();
                    WriteString("try {");
                    Spacing++;
                    WriteNode(tryStatement.Block);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    WriteNode(tryStatement.Handler);
                    if (tryStatement.Finalizer != null)
                    {
                        WriteString("finally {");
                        Spacing++;
                        WriteNode(tryStatement.Finalizer);
                        Spacing--;
                        NewLineOrBackspace();
                        WriteString("}");
                    }
                    break;
                //catch 语句
                case Nodes.CatchClause:
                    var catchClause = node as CatchClause;
                    WriteString("catch (");
                    WriteNode(catchClause.Param);
                    WriteString("){");
                    Spacing++;
                    WriteNode(catchClause.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                //throw 语句
                case Nodes.ThrowStatement:
                    var throwStatement = node as ThrowStatement;
                    WriteString("throw ");
                    WriteNode(throwStatement.Argument);
                    break;
                //空语句";"，直接丢弃
                case Nodes.EmptyStatement:
                    break;
                //break 语句
                case Nodes.BreakStatement:
                    var breakStatement = node as BreakStatement;
                    NewLine();
                    WriteString("break");
                    if (breakStatement.Label != null)
                    {
                        WriteString(" ");
                        WriteNode(breakStatement.Label);
                    }
                    WriteString(";");
                    NewLine();
                    break;
                //switch 语句
                case Nodes.SwitchStatement:
                    var switchStatement = node as SwitchStatement;
                    NewLine();
                    WriteString("switch ( ");
                    WriteNode(switchStatement.Discriminant);
                    WriteString(" ){");
                    Spacing++;
                    NewLine();
                    WriteNode(switchStatement.Cases);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                //switch case
                case Nodes.SwitchCase:
                    var switchCase = node as SwitchCase;
                    if (switchCase.Test == null)
                    {
                        WriteString("default :");
                    }
                    else
                    {
                        WriteString("case ");
                        WriteNode(switchCase.Test);
                        WriteString(" :");
                    }
                    Spacing++;
                    NewLine();
                    WriteNode(switchCase.Consequent);
                    Spacing--;
                    NewLineOrBackspace();
                    break;
                //While循环
                case Nodes.WhileStatement:
                    var whileStatement = node as WhileStatement;
                    NewLine();
                    WriteString("while (");
                    WriteNode(whileStatement.Test);
                    WriteString("){");
                    Spacing++;
                    NewLine();
                    WriteNode(whileStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                //continue 语句
                case Nodes.ContinueStatement:
                    NewLine();
                    var continueStatement = node as ContinueStatement;
                    WriteString("continue");
                    if (continueStatement.Label != null)
                    {
                        WriteString(" ");
                        WriteNode(continueStatement.Label);
                    }
                    WriteString(";");
                    break;
                //忽略Debugger
                case Nodes.DebuggerStatement:
                    break;
                //js 中goto语法  标签
                case Nodes.LabeledStatement:
                    var labeledStatement = node as LabeledStatement;
                    NewLine();
                    WriteNode(labeledStatement.Label);
                    WriteString(":");
                    NewLine();
                    WriteNode(labeledStatement.Body);
                    break;
                //do while语句
                case Nodes.DoWhileStatement:
                    var doWhileStatement = node as DoWhileStatement;
                    NewLine();
                    WriteString("do{");
                    Spacing++;
                    WriteNode(doWhileStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}while(");
                    WriteNode(doWhileStatement.Test);
                    WriteString(");");
                    break;
                //with 语句
                case Nodes.WithStatement:
                    var withStatement = node as WithStatement;
                    WriteString("with (");
                    WriteNode(withStatement.Object);
                    WriteString("){");
                    Spacing++;
                    WriteNode(withStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                //for of 
                case Nodes.ForOfStatement:
                    var forOfStatement = node as ForOfStatement;
                    NewLine();
                    WriteString("for(");
                    WriteNode(forOfStatement.Left, Options.VariableDeclarationNotNewLine | Options.VariableDeclaratorNotBranch);
                    WriteString(" of ");
                    WriteNode(forOfStatement.Right);
                    WriteString("){");
                    Spacing++;
                    WriteNode(forOfStatement.Body);
                    Spacing--;
                    NewLineOrBackspace();
                    WriteString("}");
                    break;
                case Nodes.Import:
                case Nodes.Program:
                case Nodes.RestElement:
                case Nodes.TemplateElement:
                case Nodes.TemplateLiteral:
                case Nodes.ArrayPattern:
                case Nodes.AssignmentPattern:
                case Nodes.SpreadElement:
                case Nodes.ObjectPattern:
                case Nodes.ArrowParameterPlaceHolder:
                case Nodes.MetaProperty:
                case Nodes.Super:
                case Nodes.TaggedTemplateExpression:
                case Nodes.YieldExpression:
                case Nodes.ArrowFunctionExpression:
                case Nodes.AwaitExpression:
                case Nodes.ClassBody:
                case Nodes.ClassDeclaration:
                case Nodes.MethodDefinition:
                case Nodes.ImportSpecifier:
                case Nodes.ImportDefaultSpecifier:
                case Nodes.ImportNamespaceSpecifier:
                case Nodes.ImportDeclaration:
                case Nodes.ExportSpecifier:
                case Nodes.ExportNamedDeclaration:
                case Nodes.ExportAllDeclaration:
                case Nodes.ExportDefaultDeclaration:
                case Nodes.ClassExpression:
                    throw new Exception($"请添加类型\"{node.Type}\"的转换实现");
                default:
                    throw new Exception($"请添加类型\"{node.Type}\"的转换");
                    //break;
            }
        }
        /// <summary>
        /// 写入操作符
        /// </summary>
        /// <param name="operatorEnum"></param>
        private void WriteOperator(Enum operatorEnum)
        {
            var binaryFieldInfo = operatorEnum.GetType().GetField(operatorEnum.ToString());
            WriteString($" {binaryFieldInfo.CustomAttributes.ElementAt(0).NamedArguments[0].TypedValue.Value} ");
        }

        /// <summary>
        /// 写入参数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="paramList"></param>
        /// <param name="options">可传入换行</param>
        private void WriteParams<T>(IEnumerable<T> paramList, Options options = Options.None) where T : Node
        {
            int num = 0;
            var nextOptions = options & ~Options.NewLine;
            foreach (var item in paramList)
            {
                if (num > 0)
                {
                    WriteString(",");
                    if (options.HasFlag(Options.NewLine))
                    {
                        NewLine();
                    }
                }
                WriteNode(item, nextOptions);
                num++;
            }
        }

        /// <summary>
        /// 换行
        /// </summary>
        public void NewLine()
        {
            if (!_newLine)
            {
                WriteString($"\r\n{GetRowHead()}");
                _newLine = true;
            }
        }
        /// <summary>
        /// 换行或者退格
        /// </summary>
        private void NewLineOrBackspace()
        {
            if (!_newLine)
            {
                WriteString($"\r\n{GetRowHead()}");
                _newLine = true;
            }
            else
            {
                Backspace();
            }
        }
        /// <summary>
        /// 加分号
        /// </summary>
        public void NewBranch()
        {
            if (!_newBranch)
            {
                WriteString(";");
                _newBranch = true;
            }
        }
        /// <summary>
        /// 退格
        /// </summary>
        private void Backspace()
        {
            if (LastGetSpacing <= 0)
            {
                return;
            }
            WriteStream.Position -= SpacingStrByteLength;
        }
        /// <summary>
        /// 获得行开始
        /// </summary>
        /// <returns></returns>
        private string GetRowHead()
        {
            LastGetSpacing = Spacing;
            if (Spacing == 0)
            {
                return string.Empty;
            }
            else if (Spacing > 2)
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < Spacing; i++)
                {
                    builder.Append(SpacingStr);
                }
                return builder.ToString();
            }
            else
            {
                string result = SpacingStr;
                for (int i = 1; i < Spacing; i++)
                {
                    result += SpacingStr;
                }
                return result;
            }
        }
        private void WriteString(string str, bool isNotBody = false)
        {
            var bs = Encoding.UTF8.GetBytes(str);
            WriteStream.Write(bs, 0, bs.Length);
            _newLine = isNotBody;
            _newBranch = isNotBody;
        }

    }
}
