﻿using LinqToDB.Expressions;
using LinqToDB.SqlQuery;
using System;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
	internal partial class MergeBuilder
	{
		internal class On : MethodCallBuilder
		{
			protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				return methodCall.Method.IsGenericMethod
					&& (LinqExtensions.OnMethodInfo1        .GetGenericMethodDefinition() == methodCall.Method.GetGenericMethodDefinition()
					 || LinqExtensions.OnMethodInfo2        .GetGenericMethodDefinition() == methodCall.Method.GetGenericMethodDefinition()
					 || LinqExtensions.OnTargetKeyMethodInfo.GetGenericMethodDefinition() == methodCall.Method.GetGenericMethodDefinition());
			}

			protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
			{
				var mergeContext = (MergeContext)builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

				var statement = mergeContext.Merge;

				if (methodCall.Arguments.Count == 2)
				{
					// On<TTarget, TSource>(IMergeableOn<TTarget, TSource> merge, Expression<Func<TTarget, TSource, bool>> matchCondition)
					var predicate = methodCall.Arguments[1];

					var condition = (LambdaExpression)predicate.Unwrap();
					var conditionExpr = builder.ConvertExpression(condition.Body.Unwrap());

					mergeContext.AddTargetParameter(condition.Parameters[0]);
					mergeContext.AddSourceParameter(condition.Parameters[1]);

					builder.BuildSearchCondition(
						new ExpressionContext(null, new[] { mergeContext.TargetContext, mergeContext.SourceContext }, condition),
						conditionExpr,
						statement.On.Conditions,
						false);
				}
				else if (methodCall.Arguments.Count == 3)
				{
					var targetKeyLambda = ((LambdaExpression)methodCall.Arguments[1].Unwrap());
					var sourceKeyLambda = ((LambdaExpression)methodCall.Arguments[2].Unwrap());

					var targetKeySelector = targetKeyLambda.Body.Unwrap();
					var sourceKeySelector = sourceKeyLambda.Body.Unwrap();

					var targetKeyContext = new ExpressionContext(buildInfo.Parent, mergeContext.TargetContext, targetKeyLambda);
					var sourceKeyContext = new JoinBuilder.InnerKeyContext(buildInfo.Parent, mergeContext.SourceContext, sourceKeyLambda);

					var mi1 = (MemberInitExpression)targetKeySelector;
					var mi2 = (MemberInitExpression)sourceKeySelector;

					for (var i = 0; i < mi1.Bindings.Count; i++)
					{
						if (mi1.Bindings[i].Member != mi2.Bindings[i].Member)
							throw new LinqException($"List of member inits does not match for entity type '{targetKeySelector.Type}'.");

						var arg1 = ((MemberAssignment)mi1.Bindings[i]).Expression;
						var arg2 = ((MemberAssignment)mi2.Bindings[i]).Expression;

						JoinBuilder.BuildJoin(builder, statement.On, targetKeyContext, arg1, sourceKeyContext, arg2);
					}
				}
				else
				{
					// OnTargetKey<TTarget>(IMergeableOn<TTarget, TTarget> merge)
					var targetType = methodCall.Method.GetGenericArguments()[0];
					var pTarget = Expression.Parameter(targetType, "t");
					var pSource = Expression.Parameter(targetType, "s");
					var targetDescriptor = builder.MappingSchema.GetEntityDescriptor(targetType);

					Expression ex = null;

					for (var i = 0; i< targetDescriptor.Columns.Count; i++)
					{
						var column = targetDescriptor.Columns[i];
						if (!column.IsPrimaryKey)
							continue;

						var expr = Expression.Equal(
							Expression.MakeMemberAccess(pTarget, column.MemberInfo),
							Expression.MakeMemberAccess(pSource, column.MemberInfo));
						ex = ex != null ? Expression.AndAlso(ex, expr) : expr;
					}

					if (ex == null)
						throw new LinqToDBException("Method OnTargetKey() needs at least one primary key column");

					var condition = Expression.Lambda(ex, pTarget, pSource);

					builder.BuildSearchCondition(
						new ExpressionContext(null, new[] { mergeContext.TargetContext, mergeContext.SourceContext }, condition),
						ex,
						statement.On.Conditions,
						false);
				}

				return mergeContext;
			}

			protected override SequenceConvertInfo Convert(
				ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo, ParameterExpression param)
			{
				return null;
			}
		}
	}
}
