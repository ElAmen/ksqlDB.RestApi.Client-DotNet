﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Kafka.DotNet.ksqlDB.KSql.Query.Visitors
{
  internal class LambdaVisitor : KSqlVisitor
  {
    private readonly StringBuilder stringBuilder;

    public LambdaVisitor(StringBuilder stringBuilder)
      : base(stringBuilder, useTableAlias: false)
    {
      this.stringBuilder = stringBuilder ?? throw new ArgumentNullException(nameof(stringBuilder));
    }

    protected override KSqlFunctionVisitor CreateKSqlFunctionVisitor()
    {
      return new KSqlFunctionLambdaVisitor(stringBuilder);
    }

    public override Expression? Visit(Expression? expression)
    {
      if (expression == null)
        return null;

      switch (expression.NodeType)
      {
        case ExpressionType.Lambda:
          base.Visit(expression);
          break;
        
        case ExpressionType.Parameter:
          VisitParameter((ParameterExpression)expression);
          break;

        default:
          base.Visit(expression);
          break;
      }

      return expression;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
      Append(node.Name);

      return base.VisitParameter(node);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
      Append("(");

      bool isFirst = true;

      foreach (var parameterExpression in node.Parameters)
      {
        if (isFirst)
          isFirst = false;
        else
          Append(", ");

        Append(parameterExpression.Name);
      }
      
      Append(") => ");

      Visit(node.Body);

      return node;
    }
  }
}