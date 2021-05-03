using System;
using System.Collections.Generic;
using System.Text;

namespace EsprimaToJs
{
    public enum Options
    {
        None = 0,
        /// <summary>
        /// 是否换行
        /// </summary>
        NewLine = 1,
        /// <summary>
        /// 是否添加分号
        /// </summary>
        NewBranch = 2,
        /// <summary>
        /// 赋值语句需要添加()
        /// </summary>
        VariableDeclaratorBrackets = 4,
        /// <summary>
        /// 创建变量时不要换行
        /// </summary>
        VariableDeclarationNotNewLine = 8,
        /// <summary>
        /// 块语句换行
        /// </summary>
        BlockStatementLine = 16,
        /// <summary>
        /// 逗号语句中不换行
        /// </summary>
        SequenceExpressionNotLine=32,
        /// <summary>
        /// 赋值中不带分号
        /// </summary>
        VariableDeclaratorNotBranch=64,
        /// <summary>
        /// 创建变量时，多个采用`,`
        /// </summary>
        VariableDeclarationComma=128,
        /// <summary>
        /// if 不换行
        /// </summary>
        IfStatementNoLine=256,
        /// <summary>
        /// 方法需要用`()`包裹
        /// </summary>
        FunctionExpressionBrackets=512,
        /// <summary>
        /// 三元运算符需要使用()包裹
        /// </summary>
        ConditionalExpressionBrackets=1024,
    }
}
