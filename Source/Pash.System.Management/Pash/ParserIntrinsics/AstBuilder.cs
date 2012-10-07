﻿using System;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation.Language;

using Extensions.String;
using Irony.Parsing;
using Pash.ParserIntrinsics;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Pash.ParserIntrinsics
{
    class AstBuilder
    {
        readonly PowerShellGrammar _grammar;

        public AstBuilder(PowerShellGrammar grammar)
        {
            this._grammar = grammar;
        }

        [Conditional("DEBUG")]
        static void VerifyTerm(ParseTreeNode parseTreeNode, BnfTerm expectedTerm, params BnfTerm[] moreExpectedTerms)
        {
            var allExpected = new[] { expectedTerm }.Concat(moreExpectedTerms);

            if (!allExpected.Where(node => parseTreeNode.Term == expectedTerm).Any())
            {
                throw new InvalidOperationException("expected '{0}' to be a '{1}'".FormatString(parseTreeNode, allExpected.ToArray().JoinString(", ")));
            }
        }

        public ScriptBlockAst BuildInteractiveInputAst(ParseTreeNode parseTreeNode)
        {
            ////        interactive_input:
            ////            script_block
            VerifyTerm(parseTreeNode, this._grammar.interactive_input);

            return BuildScriptBlockAst(parseTreeNode.ChildNodes.Single());
        }

        ScriptBlockAst BuildScriptBlockAst(ParseTreeNode parseTreeNode)
        {
            ////        script_block:
            ////            param_block_opt   statement_terminators_opt    script_block_body_opt
            VerifyTerm(parseTreeNode, this._grammar.script_block);

            ParamBlockAst paramBlockAst = null;
            StatementBlockAst statementBlockAst = null;

            if (parseTreeNode.ChildNodes.Any())
            {
                // Note that I used First() and Last() to make it deal with the fact that both are optional
                if (parseTreeNode.ChildNodes.First().Term == this._grammar.param_block)
                {
                    paramBlockAst = BuildParamBlockAst(parseTreeNode.ChildNodes.First());
                }

                if (parseTreeNode.ChildNodes.Last().Term == this._grammar.script_block_body)
                {
                    statementBlockAst = BuildScriptBlockBodyAst(parseTreeNode.ChildNodes.Last());
                }
            }

            return new ScriptBlockAst(
                new ScriptExtent(parseTreeNode),
                paramBlockAst,
                statementBlockAst,
                false
                );
        }

        ParamBlockAst BuildParamBlockAst(ParseTreeNode parseTreeNode)
        {
            ////        param_block:
            ////            new_lines_opt   attribute_list_opt   new_lines_opt   param   new_lines_opt
            ////                    (   parameter_list_opt   new_lines_opt   )
            VerifyTerm(parseTreeNode, this._grammar.param_block);

            throw new NotImplementedException(parseTreeNode.ToString());
        }

        ScriptBlockExpressionAst BuildScriptBlockExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        script_block_expression:
            ////            {   new_lines_opt   script_block   new_lines_opt   }
            VerifyTerm(parseTreeNode, this._grammar.script_block_expression);

            var scriptBlockNode = parseTreeNode.ChildNodes[1];

            return new ScriptBlockExpressionAst(
                new ScriptExtent(scriptBlockNode),
                BuildScriptBlockAst(scriptBlockNode)
                );
        }

        StatementBlockAst BuildScriptBlockBodyAst(ParseTreeNode parseTreeNode)
        {
            ////        script_block_body:
            ////            named_block_list
            ////            statement_list
            VerifyTerm(parseTreeNode, this._grammar.script_block_body);

            parseTreeNode = parseTreeNode.ChildNodes.Single();

            if (parseTreeNode.Term == this._grammar.named_block_list)
            {
                return BuildNamedBlockListAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.statement_list)
            {
                return BuildStatementListAst(parseTreeNode);
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        StatementBlockAst BuildStatementListAst(ParseTreeNode parseTreeNode)
        {
            ////        statement_list:
            ////            statement
            ////            statement_list   statement

            // HACK: I used
            //        statement_list:
            //            statement
            //            statement_list   statement_terminator    statement

            IEnumerable<StatementAst> statements = parseTreeNode.ChildNodes.Select(BuildStatementAst);
            return new StatementBlockAst(new ScriptExtent(parseTreeNode), statements, new TrapStatementAst[] { });
        }

        StatementBlockAst BuildNamedBlockListAst(ParseTreeNode parseTreeNode)
        {
            throw new NotImplementedException();
        }

        StatementAst BuildStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        statement:
            ////            if_statement
            ////            label_opt   labeled_statement
            ////            function_statement
            ////            flow_control_statement   statement_terminator
            ////            trap_statement
            ////            try_statement
            ////            data_statement
            ////            pipeline   statement_terminator(_opt)

            VerifyTerm(parseTreeNode, this._grammar.statement);

            parseTreeNode = parseTreeNode.ChildNodes.Single();

            if (parseTreeNode.Term == this._grammar.if_statement)
            {
                return BuildIfStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar._statement_labeled_statement)
            {
                return BuildLabeledStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.function_statement)
            {
                return BuildFunctionStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar._statement_flow_control_statement)
            {
                return BuildFlowControlStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.trap_statement)
            {
                return BuildTrapStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.try_statement)
            {
                return BuildTryStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.data_statement)
            {
                return BuildDataStatementAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar._statement_pipeline)
            {
                return BuildStatementPipelineAst(parseTreeNode);
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        StatementAst BuildFunctionStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        function_statement:
            ////            function   new_lines_opt   function_name   function_parameter_declaration_opt   {   script_block   }
            ////            filter   new_lines_opt   function_name   function_parameter_declaration_opt   {   script_block   }

            throw new NotImplementedException();
        }

        StatementAst BuildFlowControlStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        flow_control_statement:
            ////            break   label_expression_opt
            ////            continue   label_expression_opt
            ////            throw    pipeline_opt
            ////            return   pipeline_opt
            ////            exit   pipeline_opt

            throw new NotImplementedException();
        }

        StatementAst BuildTrapStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        trap_statement:
            ////            trap  new_lines_opt   type_literal_opt   new_lines_opt   statement_block

            throw new NotImplementedException();
        }

        StatementAst BuildTryStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        try_statement:
            ////            try   statement_block   catch_clauses
            ////            try   statement_block   finally_clause
            ////            try   statement_block   catch_clauses   finally_clause

            throw new NotImplementedException();
        }

        StatementAst BuildDataStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        data_statement:
            ////            data    new_lines_opt   data_name   data_commands_allowed_opt   statement_block

            throw new NotImplementedException();
        }

        StatementAst BuildStatementPipelineAst(ParseTreeNode parseTreeNode)
        {
            //          _statement_pipeline:
            //              pipeline   /* statement_terminator */

            VerifyTerm(parseTreeNode, this._grammar._statement_pipeline);

            return BuildPipelineAst(parseTreeNode.ChildNodes.Single());
        }

        StatementAst BuildLabeledStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        labeled_statement:
            ////            switch_statement
            ////            foreach_statement
            ////            for_statement
            ////            while_statement
            ////            do_statement

            throw new NotImplementedException();
        }

        StatementAst BuildIfStatementAst(ParseTreeNode parseTreeNode)
        {
            ////        if_statement:
            ////            if   new_lines_opt   (   new_lines_opt   pipeline   new_lines_opt   )   statement_block elseif_clauses_opt   else_clause_opt

            throw new NotImplementedException();
        }

        PipelineBaseAst BuildPipelineAst(ParseTreeNode parseTreeNode)
        {
            //          pipeline:
            //            assignment_expression
            //            _pipeline_expression
            //            _pipeline_command

            VerifyTerm(parseTreeNode, this._grammar.pipeline);
            var childNode = parseTreeNode.ChildNodes.Single();

            if (childNode.Term == this._grammar.assignment_expression)
            {
                throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
            }

            if (childNode.Term == this._grammar._pipeline_expression)
            {
                return BuildPipelineExpressionAst(childNode);
            }

            if (childNode.Term == this._grammar._pipeline_command)
            {
                return BuildPipelineCommandAst(childNode);
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        PipelineBaseAst BuildPipelineExpressionAst(ParseTreeNode parseTreeNode)
        {
            //        _pipeline_expression:
            //            expression   redirections_opt  pipeline_tail_opt
            VerifyTerm(parseTreeNode, this._grammar._pipeline_expression);

            var commandExpressionAst = new CommandExpressionAst(
                new ScriptExtent(parseTreeNode.ChildNodes[0]),
                BuildExpressionAst(parseTreeNode.ChildNodes[0]), null
                );

            if (parseTreeNode.ChildNodes.Count == 1)
            {
                return new PipelineAst(
                    new ScriptExtent(parseTreeNode),
                    commandExpressionAst
                    );
            }
            if (parseTreeNode.ChildNodes.Count == 2 && parseTreeNode.ChildNodes[1].Term == this._grammar.pipeline_tail)
            {
                var pipelineTail = GetPipelineTailCommandList(parseTreeNode.ChildNodes[1]).ToList();
                pipelineTail.Insert(0, commandExpressionAst);
                return new PipelineAst(new ScriptExtent(parseTreeNode), pipelineTail);
            }

            throw new NotImplementedException(parseTreeNode.ToString());

        }

        PipelineBaseAst BuildPipelineCommandAst(ParseTreeNode parseTreeNode)
        {
            //        _pipeline_command:
            //            command   pipeline_tail_opt

            VerifyTerm(parseTreeNode, this._grammar._pipeline_command);

            CommandAst commandAst = BuildCommandAst(parseTreeNode.ChildNodes[0]);
            if (parseTreeNode.ChildNodes.Count == 1)
            {
                return new PipelineAst(new ScriptExtent(parseTreeNode), commandAst);
            }
            if (parseTreeNode.ChildNodes.Count == 2)
            {
                var pipelineTail = GetPipelineTailCommandList(parseTreeNode.ChildNodes[1]).ToList();
                pipelineTail.Insert(0, commandAst);
                return new PipelineAst(new ScriptExtent(parseTreeNode), pipelineTail);
            }
            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        IEnumerable<CommandBaseAst> GetPipelineTailCommandList(ParseTreeNode parseTreeNode)
        {
            ////        pipeline_tail:
            ////            |   new_lines_opt   command
            ////            |   new_lines_opt   command   pipeline_tail

            VerifyTerm(parseTreeNode, this._grammar.pipeline_tail);

            return parseTreeNode.ChildNodes.Skip(1).Select(BuildCommandAst);
        }

        ExpressionAst BuildExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        expression:
            ////            logical_expression
            VerifyTerm(parseTreeNode, this._grammar.expression);

            return BuildLogicalExpressionAst(parseTreeNode.ChildNodes.Single());
        }

        ExpressionAst BuildLogicalExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        logical_expression:
            ////            bitwise_expression
            ////            logical_expression   _and   new_lines_opt   bitwise_expression
            ////            logical_expression   _or   new_lines_opt   bitwise_expression
            ////            logical_expression   _xor   new_lines_opt   bitwise_expression
            VerifyTerm(parseTreeNode, this._grammar.logical_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.bitwise_expression)
            {
                return BuildBitwiseExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildBitwiseExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        bitwise_expression:
            ////            comparison_expression
            ////            bitwise_expression   _band   new_lines_opt   comparison_expression
            ////            bitwise_expression   _bor   new_lines_opt   comparison_expression
            ////            bitwise_expression   _bxor   new_lines_opt   comparison_expression
            VerifyTerm(parseTreeNode, this._grammar.bitwise_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.comparison_expression)
            {
                return BuildComparisonExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildComparisonExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        comparison_expression:
            ////            additive_expression
            ////            comparison_expression   comparison_operator   new_lines_opt   additive_expression
            VerifyTerm(parseTreeNode, this._grammar.comparison_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.additive_expression)
            {
                return BuildAdditiveExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildAdditiveExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        additive_expression:
            ////            multiplicative_expression
            ////            additive_expression   +   new_lines_opt   multiplicative_expression
            ////            additive_expression   dash   new_lines_opt   multiplicative_expression
            VerifyTerm(parseTreeNode, this._grammar.additive_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.multiplicative_expression)
            {
                return BuildMultiplicativeExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            else
            {
                var leftOperand = parseTreeNode.ChildNodes[0];
                var operatorNode = parseTreeNode.ChildNodes[1];
                var rightOperand = parseTreeNode.ChildNodes[2];

                return new BinaryExpressionAst(
                    new ScriptExtent(parseTreeNode),
                    BuildAdditiveExpressionAst(leftOperand),
                    operatorNode.Term == this._grammar.dash ? TokenKind.Minus : TokenKind.Plus,
                    BuildMultiplicativeExpressionAst(rightOperand),
                    new ScriptExtent(operatorNode)
                    );
            }
        }

        ExpressionAst BuildMultiplicativeExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        multiplicative_expression:
            ////            format_expression
            ////            multiplicative_expression   *   new_lines_opt   format_expression
            ////            multiplicative_expression   /   new_lines_opt   format_expression
            ////            multiplicative_expression   %   new_lines_opt   format_expression
            VerifyTerm(parseTreeNode, this._grammar.multiplicative_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.format_expression)
            {
                return BuildFormatExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildFormatExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        format_expression:
            ////            range_expression
            ////            format_expression   format_operator    new_lines_opt   range_expression
            VerifyTerm(parseTreeNode, this._grammar.format_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.range_expression)
            {
                return BuildRangeExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildRangeExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        range_expression:
            ////            array_literal_expression
            ////            range_expression   ..   new_lines_opt   array_literal_expression
            VerifyTerm(parseTreeNode, this._grammar.range_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.array_literal_expression)
            {
                return BuildArrayLiteralExpressionAst(parseTreeNode.ChildNodes.Single());
            }
            else
            {
                var startNode = parseTreeNode.ChildNodes[0];
                var dotDotNode = parseTreeNode.ChildNodes[1];
                var endNode = parseTreeNode.ChildNodes[2];

                return new BinaryExpressionAst(
                    new ScriptExtent(parseTreeNode),
                    BuildRangeExpressionAst(startNode),
                    TokenKind.DotDot,
                    BuildArrayLiteralExpressionAst(endNode),
                    new ScriptExtent(dotDotNode)
                    );
            }
        }

        ExpressionAst BuildArrayLiteralExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        array_literal_expression:
            ////            unary_expression
            ////            unary_expression   ,    new_lines_opt   array_literal_expression

            VerifyTerm(parseTreeNode, this._grammar.array_literal_expression);
            VerifyTerm(parseTreeNode.ChildNodes[0], this._grammar.unary_expression);

            var unaryExpression = BuildUnaryExpressionAst(parseTreeNode.ChildNodes.First());

            if (parseTreeNode.ChildNodes.Count == 1)
            {
                return unaryExpression;
            }
            if (parseTreeNode.ChildNodes.Count == 3)
            {
                List<ExpressionAst> elements = new List<ExpressionAst>();
                elements.Add(unaryExpression);

                var remaining = BuildArrayLiteralExpressionAst(parseTreeNode.ChildNodes[2]);
                if (remaining is ArrayLiteralAst)
                {
                    elements.AddRange(((ArrayLiteralAst)remaining).Elements);
                }
                else
                {
                    elements.Add(remaining);
                }

                return new ArrayLiteralAst(new ScriptExtent(parseTreeNode), elements);
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        ExpressionAst BuildUnaryExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        unary_expression:
            ////            primary_expression
            ////            expression_with_unary_operator
            VerifyTerm(parseTreeNode, this._grammar.unary_expression);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.primary_expression)
            {
                return BuildPrimaryExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.expression_with_unary_operator)
            {
                return BuildExpressionWithUnaryOperatorAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ExpressionAst BuildExpressionWithUnaryOperatorAst(ParseTreeNode parseTreeNode)
        {
            ////        expression_with_unary_operator:
            ////            ,   new_lines_opt   unary_expression
            ////            -not   new_lines_opt   unary_expression
            ////            !   new_lines_opt   unary_expression
            ////            -bnot   new_lines_opt   unary_expression
            ////            +   new_lines_opt   unary_expression
            ////            dash   new_lines_opt   unary_expression
            ////            pre_increment_expression
            ////            pre_decrement_expression
            ////            cast_expression
            ////            -split   new_lines_opt   unary_expression
            ////            -join   new_lines_opt   unary_expression
            VerifyTerm(parseTreeNode, this._grammar.expression_with_unary_operator);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.dash)
            {
                var expression = BuildUnaryExpressionAst(parseTreeNode.ChildNodes[1]);
                ConstantExpressionAst constantExpressionAst = expression as ConstantExpressionAst;
                if (constantExpressionAst == null)
                {
                    throw new NotImplementedException(parseTreeNode.ToString());
                }
                else
                {
                    if (constantExpressionAst.StaticType == typeof(int))
                    {
                        return new ConstantExpressionAst(new ScriptExtent(parseTreeNode), 0 - ((int)constantExpressionAst.Value));
                    }
                    else
                    {
                        throw new NotImplementedException(parseTreeNode.ToString());
                    }
                }
            }

            throw new NotImplementedException(parseTreeNode.ToString());
        }

        ExpressionAst BuildPrimaryExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        primary_expression:
            ////            value
            ////            member_access
            ////            element_access
            ////            invocation_expression
            ////            post_increment_expression
            ////            post_decrement_expression
            VerifyTerm(parseTreeNode, this._grammar.primary_expression);

            parseTreeNode = parseTreeNode.ChildNodes.Single();

            if (parseTreeNode.Term == this._grammar.value)
            {
                return BuildValueExpressionAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.member_access)
            {
                return BuildMemberExpressionAst(parseTreeNode);
            }

            throw new NotImplementedException(parseTreeNode.ToString());
        }

        MemberExpressionAst BuildMemberExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        member_access: 
            ////            primary_expression   .   member_name
            ////            primary_expression   ::   member_name
            VerifyTerm(parseTreeNode, this._grammar.member_access);

            var typeExpressionAst = BuildPrimaryExpressionAst(parseTreeNode.ChildNodes[0]);
            bool @static = parseTreeNode.ChildNodes[1].FindTokenAndGetText() == "::";
            var memberName = BuildMemberNameAst(parseTreeNode.ChildNodes[2]);

            return new MemberExpressionAst(new ScriptExtent(parseTreeNode), typeExpressionAst, memberName, @static);
        }

        CommandElementAst BuildMemberNameAst(ParseTreeNode parseTreeNode)
        {
            ////        member_name:
            ////            simple_name
            ////            string_literal
            ////            string_literal_with_subexpression
            ////            expression_with_unary_operator
            ////            value
            VerifyTerm(parseTreeNode, this._grammar.member_name);

            parseTreeNode = parseTreeNode.ChildNodes.Single();

            if (parseTreeNode.Term == this._grammar.simple_name)
            {
                return BuildSimpleNameAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.string_literal)
            {
                return BuildStringLiteralAst(parseTreeNode);
            }


            if (parseTreeNode.Term == this._grammar.string_literal_with_subexpression)
            {
                return BuildStringLiteralWithSubexpressionAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.expression_with_unary_operator)
            {
                return BuildExpressionWithUnaryOperatorAst(parseTreeNode);
            }

            if (parseTreeNode.Term == this._grammar.value)
            {
                return BuildValueExpressionAst(parseTreeNode);
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        CommandElementAst BuildStringLiteralWithSubexpressionAst(ParseTreeNode parseTreeNode)
        {
            throw new NotImplementedException(parseTreeNode.ToString());
        }

        ExpressionAst BuildValueExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        value:
            ////            parenthesized_expression
            ////            sub_expression
            ////            array_expression
            ////            script_block_expression
            ////            hash_literal_expression
            ////            literal
            ////            type_literal
            ////            variable
            VerifyTerm(parseTreeNode, this._grammar.value);

            var childNode = parseTreeNode.ChildNodes.Single();

            if (childNode.Term == this._grammar.parenthesized_expression)
            {
                return BuildParenthesizedExpressionAst(childNode);
            }

            if (childNode.Term == this._grammar.sub_expression)
            {
                throw new NotImplementedException(childNode.Term.Name);
            }

            if (childNode.Term == this._grammar.array_expression)
            {
                throw new NotImplementedException(childNode.Term.Name);
            }

            if (childNode.Term == this._grammar.script_block_expression)
            {
                return BuildScriptBlockExpressionAst(childNode);
            }

            if (childNode.Term == this._grammar.hash_literal_expression)
            {
                return BuildHashLiteralExpressionAst(childNode);
            }

            if (childNode.Term == this._grammar.literal)
            {
                return BuildLiteralAst(childNode);
            }

            if (childNode.Term == this._grammar.type_literal)
            {
                return BuildTypeLiteralAst(childNode);
            }

            if (childNode.Term == this._grammar.variable)
            {
                return BuildVariableAst(childNode);
            }

            throw new InvalidOperationException(childNode.Term.Name);
        }

        VariableExpressionAst BuildVariableAst(ParseTreeNode parseTreeNode)
        {
            ////        variable:
            ////            $$
            ////            $?
            ////            $^
            ////            $   variable_scope_opt  variable_characters
            ////            @   variable_scope_opt   variable_characters
            ////            braced_variable
            VerifyTerm(parseTreeNode, this._grammar.variable);

            var match = this._grammar.variable.Expression.Match(parseTreeNode.Token.Text);

            if (match.Groups[this._grammar._variable_ordinary_variable.Name].Success)
            {
                return new VariableExpressionAst(new ScriptExtent(parseTreeNode), parseTreeNode.Token.Text.Substring(1), false);
            }

            throw new NotImplementedException(parseTreeNode.ToString());
        }

        TypeExpressionAst BuildTypeLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        type_literal:
            ////            [    type_spec   ]
            VerifyTerm(parseTreeNode, this._grammar.type_literal);

            parseTreeNode = parseTreeNode.ChildNodes[1];

            ////        type_spec:
            ////            array_type_name    dimension_opt   ]
            ////            generic_type_name   generic_type_arguments   ]
            ////            type_name
            ////        dimension:
            ////            ,
            ////            dimension   ,

            VerifyTerm(parseTreeNode, this._grammar.type_spec);

            var firstNode = parseTreeNode.ChildNodes.First();

            var typeNameNode = parseTreeNode.ChildNodes.First();

            if (typeNameNode.Term == this._grammar.type_name)
            {
                return new TypeExpressionAst(new ScriptExtent(parseTreeNode), new TypeName(
                    typeNameNode.Token.Text
                    ));
            }

            throw new NotImplementedException(typeNameNode.ToString());
        }

        HashtableAst BuildHashLiteralExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        hash_literal_expression:
            ////            @{   new_lines_opt   hash_literal_body_opt   new_lines_opt   }

            VerifyTerm(parseTreeNode, this._grammar.hash_literal_expression);

            List<Tuple<ExpressionAst, StatementAst>> hashEntries = new List<Tuple<ExpressionAst, StatementAst>>();

            if (parseTreeNode.ChildNodes.Count == 3)
            {

                var hashLiteralBodyParseTreeNode = parseTreeNode.ChildNodes[1];

                ////        hash_literal_body:
                ////            hash_entry
                ////            hash_literal_body   statement_terminators   hash_entry

                VerifyTerm(hashLiteralBodyParseTreeNode, this._grammar.hash_literal_body);

                for (int i = 0; i < hashLiteralBodyParseTreeNode.ChildNodes.Count; i++)
                {
                    var hashEntryParseTreeNode = hashLiteralBodyParseTreeNode.ChildNodes[i];

                    VerifyTerm(hashEntryParseTreeNode, this._grammar.hash_entry);

                    ////        hash_entry:
                    ////            key_expression   =   new_lines_opt   statement
                    var keyExpressionAst = BuildKeyExpressionAst(hashEntryParseTreeNode.ChildNodes[0]);
                    var valueAst = BuildStatementAst(hashEntryParseTreeNode.ChildNodes[2]);

                    hashEntries.Add(new Tuple<ExpressionAst, StatementAst>(keyExpressionAst, valueAst));
                }
            }

            return new HashtableAst(new ScriptExtent(parseTreeNode), hashEntries);
        }

        ExpressionAst BuildKeyExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        key_expression:
            ////            simple_name
            ////            unary_expression

            VerifyTerm(parseTreeNode, this._grammar.key_expression);

            var childParseTreeNode = parseTreeNode.ChildNodes.Single();

            if (childParseTreeNode.Term == this._grammar.simple_name)
            {
                return BuildSimpleNameAst(childParseTreeNode);
            }

            if (childParseTreeNode.Term == this._grammar.unary_expression)
            {
                return BuildUnaryExpressionAst(childParseTreeNode);
            }

            throw new InvalidOperationException(childParseTreeNode.ToString());
        }

        ExpressionAst BuildSimpleNameAst(ParseTreeNode parseTreeNode)
        {
            ////        simple_name:
            ////            simple_name_first_char   simple_name_chars
            ////        simple_name_chars:
            ////            simple_name_char
            ////            simple_name_chars   simple_name_char

            VerifyTerm(parseTreeNode, this._grammar.simple_name);
            return new StringConstantExpressionAst(new ScriptExtent(parseTreeNode), parseTreeNode.Token.Text, StringConstantType.BareWord);
        }

        ParenExpressionAst BuildParenthesizedExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        parenthesized_expression:
            ////            (   new_lines_opt   pipeline   new_lines_opt   )
            VerifyTerm(parseTreeNode, this._grammar.parenthesized_expression);

            return new ParenExpressionAst(
                new ScriptExtent(parseTreeNode),
                BuildPipelineAst(parseTreeNode.ChildNodes[1])
                );
        }

        ConstantExpressionAst BuildLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        literal:
            ////            integer_literal
            ////            real_literal
            ////            string_literal
            VerifyTerm(parseTreeNode, this._grammar.literal);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.integer_literal)
            {
                return BuildIntegerLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.string_literal)
            {
                return BuildStringLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ConstantExpressionAst BuildIntegerLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        integer_literal:
            ////            decimal_integer_literal
            ////            hexadecimal_integer_literal
            VerifyTerm(parseTreeNode, this._grammar.integer_literal);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.decimal_integer_literal)
            {
                return BuildDecimalIntegerLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.hexadecimal_integer_literal)
            {
                return BuildHexaecimalIntegerLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        ConstantExpressionAst BuildDecimalIntegerLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        decimal_integer_literal:
            ////            decimal_digits   numeric_type_suffix_opt   numeric_multiplier_opt
            VerifyTerm(parseTreeNode, this._grammar.decimal_integer_literal);
            var matches = Regex.Match(parseTreeNode.FindTokenAndGetText(), this._grammar.decimal_integer_literal.Pattern, RegexOptions.IgnoreCase);
            string value = matches.Groups[this._grammar.decimal_digits.Name].Value;

            return new ConstantExpressionAst(new ScriptExtent(parseTreeNode), Convert.ToInt32(value, 10));
        }

        ConstantExpressionAst BuildHexaecimalIntegerLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        hexadecimal_integer_literal:
            ////            0x   hexadecimal_digits   long_type_suffix_opt   numeric_multiplier_opt
            VerifyTerm(parseTreeNode, this._grammar.hexadecimal_integer_literal);

            var matches = Regex.Match(parseTreeNode.FindTokenAndGetText(), this._grammar.hexadecimal_integer_literal.Pattern, RegexOptions.IgnoreCase);
            string value = matches.Groups[this._grammar.hexadecimal_digits.Name].Value;

            return new ConstantExpressionAst(new ScriptExtent(parseTreeNode), Convert.ToInt32(value, 16));
        }

        StringConstantExpressionAst BuildStringLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        string_literal:
            ////            expandable_string_literal
            ////            expandable_here_string_literal
            ////            verbatim_string_literal
            ////            verbatim_here_string_literal
            VerifyTerm(parseTreeNode, this._grammar.string_literal);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.expandable_string_literal)
            {
                return BuildExpandableStringLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            if (parseTreeNode.ChildNodes[0].Term == this._grammar.verbatim_string_literal)
            {
                return BuildVerbatimStringLiteralAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        StringConstantExpressionAst BuildExpandableStringLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        expandable_string_literal:
            ////            double_quote_character   expandable_string_characters_opt   dollars_opt   double_quote_character
            var matches = Regex.Match(parseTreeNode.FindTokenAndGetText(), this._grammar.expandable_string_literal.Pattern, RegexOptions.IgnoreCase);
            string value = matches.Groups[this._grammar.expandable_string_characters.Name].Value +
                matches.Groups[this._grammar.dollars.Name].Value
                ;

            return new StringConstantExpressionAst(new ScriptExtent(parseTreeNode), value, StringConstantType.DoubleQuoted);
        }

        StringConstantExpressionAst BuildVerbatimStringLiteralAst(ParseTreeNode parseTreeNode)
        {
            ////        verbatim_string_literal:
            ////            single_quote_character   verbatim_string_characters_opt   single_quote_char [sic]
            VerifyTerm(parseTreeNode, this._grammar.verbatim_string_literal);

            var matches = Regex.Match(parseTreeNode.FindTokenAndGetText(), this._grammar.verbatim_string_literal.Pattern, RegexOptions.IgnoreCase);
            string value = matches.Groups[this._grammar.verbatim_string_characters.Name].Value;

            return new StringConstantExpressionAst(new ScriptExtent(parseTreeNode), value, StringConstantType.SingleQuoted);
        }

        CommandAst BuildCommandAst(ParseTreeNode parseTreeNode)
        {
            ////        command:
            ////            command_name   command_elements_opt
            ////            command_invocation_operator   command_module_opt  command_name_expr   command_elements_opt
            VerifyTerm(parseTreeNode, this._grammar.command);

            if (parseTreeNode.ChildNodes[0].Term == this._grammar._command_simple)
            {
                parseTreeNode = parseTreeNode.ChildNodes.Single();
                var commandElements = new List<CommandElementAst>();

                commandElements.Add(BuildCommandNameAst(parseTreeNode.ChildNodes[0]));

                if (parseTreeNode.ChildNodes.Count == 2)
                {
                    foreach (var commandElementNode in parseTreeNode.ChildNodes[1].ChildNodes)
                    {
                        commandElements.Add(BuildCommandElementAst(commandElementNode));
                    }
                }

                return new CommandAst(
                    new ScriptExtent(parseTreeNode),
                    commandElements,
                    TokenKind.Unknown,
                    null);

            }
            if (parseTreeNode.ChildNodes[0].Term == this._grammar._command_invocation)
            {
                parseTreeNode = parseTreeNode.ChildNodes.Single();
                throw new NotImplementedException(parseTreeNode.ChildNodes[0].Term.Name);
            }

            throw new InvalidOperationException(parseTreeNode.ChildNodes[0].Term.Name);
        }

        CommandElementAst BuildCommandNameAst(ParseTreeNode parseTreeNode)
        {
            ////        command_name:
            ////            generic_token
            ////            generic_token_with_subexpr
            VerifyTerm(parseTreeNode, this._grammar.command_name);

            if (parseTreeNode.ChildNodes.Single().Term == this._grammar.generic_token)
            {
                return BuildGenericTokenAst(parseTreeNode.ChildNodes.Single());
            }

            throw new NotImplementedException(this.ToString());
        }

        StringConstantExpressionAst BuildGenericTokenAst(ParseTreeNode parseTreeNode)
        {
            ////        generic_token:
            ////            generic_token_parts
            VerifyTerm(parseTreeNode, this._grammar.generic_token);

            ////        generic_token_part:
            ////            expandable_string_literal
            ////            verbatim_here_string_literal
            ////            variable
            ////            generic_token_char

            // I'm confused by the idea that a generic_token could have several of these things smushed together, like this:
            //    PS> $x = "Get-"
            //    PS> $x"ChildItem"     # really? This gives an error in PowerShell. But:
            //    PS> & $x"ChildItem"   # works!
            //    PS> g"et-childite"m   # also works

            var match = this._grammar.generic_token.Expression.Match(parseTreeNode.Token.Text);

            if (match.Groups[this._grammar.expandable_string_literal.Name].Success) throw new NotImplementedException(parseTreeNode.ToString());
            //if (match.Groups[this._grammar.verbatim_here_string_literal.Name].Success) throw new NotImplementedException(parseTreeNode.ToString());
            if (match.Groups[this._grammar.variable.Name].Success) throw new NotImplementedException(parseTreeNode.ToString());

            return new StringConstantExpressionAst(new ScriptExtent(parseTreeNode), parseTreeNode.Token.Text, StringConstantType.BareWord);
        }

        CommandElementAst BuildCommandElementAst(ParseTreeNode parseTreeNode)
        {
            ////        command_element:
            ////            command_parameter
            ////            command_argument
            ////            redirection
            VerifyTerm(parseTreeNode, this._grammar.command_element);

            var childNode = parseTreeNode.ChildNodes.Single();

            if (childNode.Term == this._grammar.command_parameter)
            {
                return BuildCommandParameterAst(childNode);
            }

            if (childNode.Term == this._grammar.command_argument)
            {
                return BuildCommandArgumentAst(childNode);
            }

            if (childNode.Term == this._grammar.redirection) throw new NotImplementedException(childNode.ToString());

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        CommandElementAst BuildCommandArgumentAst(ParseTreeNode parseTreeNode)
        {
            ////        command_argument:
            ////            command_name_expr

            VerifyTerm(parseTreeNode, this._grammar.command_argument);

            return BuildCommandNameExpressionAst(parseTreeNode.ChildNodes.Single());
        }

        CommandElementAst BuildCommandNameExpressionAst(ParseTreeNode parseTreeNode)
        {
            ////        command_name_expr:
            ////            command_name
            ////            primary_expression

            VerifyTerm(parseTreeNode, this._grammar.command_name_expr);

            if (parseTreeNode.ChildNodes.Single().Term == this._grammar.command_name)
            {
                return BuildCommandNameAst(parseTreeNode.ChildNodes.Single());
            }

            if (parseTreeNode.ChildNodes.Single().Term == this._grammar.primary_expression)
            {
                return BuildPrimaryExpressionAst(parseTreeNode.ChildNodes.Single());
            }

            throw new InvalidOperationException(parseTreeNode.ToString());
        }

        CommandParameterAst BuildCommandParameterAst(ParseTreeNode parseTreeNode)
        {
            ////        command_parameter:
            ////            dash   first_parameter_char   parameter_chars   colon_opt

            VerifyTerm(parseTreeNode, this._grammar.command_parameter);

            var match = this._grammar.command_parameter.Expression.Match(parseTreeNode.Token.Text);
            var parameterName = match.Groups[this._grammar._parameter_name.Name].Value;

            bool colon = match.Groups[this._grammar.colon.Name].Success;

            // to match PowerShell's behavior, we have to bump command_parameter to be a nonterminal. Later.
            // 
            // Try parsing this to see:
            //    x -y:$z
            //
            //    PS> $ast.EndBlock.Statements[0].PipelineElements[0].CommandElements[1]
            //
            //    ParameterName : y
            //    Argument      : $z
            //    ErrorPosition : -y:
            //    Extent        : -y:$z
            //    Parent        : x -y:$z

            if (colon) throw new NotImplementedException("can't parse colon parameters");

            return new CommandParameterAst(new ScriptExtent(parseTreeNode), parameterName, null, new ScriptExtent(parseTreeNode));
        }
    }
}