﻿using System.Linq;
using LinqToDB;
using LinqToDB.Mapping;
using NUnit.Framework;
using LinqToDB.DataProvider.SQLite;
using System;
using Tests.Model;
using System.Data.Common;

namespace Tests.Linq
{
	[TestFixture]
	public partial class FullTextTests : TestBase
	{
		// TODO: FTS5 tests not executed against database due to missing support in used providers

		#region Mappings
		public class FtsTable
		{
			public string text1 { get; set; }

			public string text2 { get; set; }
		}

		public enum SQLiteFTS
		{
			FTS3,
			FTS4,
			FTS5
		}

		private MappingSchema SetupFtsMapping(SQLiteFTS type)
		{
			var ms = new MappingSchema();

			ms.GetFluentMappingBuilder()
				.Entity<FtsTable>()
				.HasTableName(type.ToString() + "_TABLE")
				.HasColumn(t => t.text1)
				.HasColumn(t => t.text2);

			return ms;
		}
		#endregion

		#region MATCH
		[Test, Category("FreeText")]
		public void MatchByTable([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>().Where(r => r.Match("something"));

				if (type != SQLiteFTS.FTS5)
				{
					var results = query.ToList();
					Assert.AreEqual(1, results.Count);
					Assert.AreEqual("looking for something?", results[0].text1);
					Assert.AreEqual("found it!", results[0].text2);
				}
				else
				{
					var sql = query.ToString();
					Assert.That(sql.Contains("[r].[FTS5_TABLE] MATCH 'something'"));
				}
			}
		}

		[Test, Category("FreeText")]
		public void MatchByColumn([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>().Where(r => r.Match(r.text1, "found"));

				if (type != SQLiteFTS.FTS5)
				{
					var results = query.ToList();
					Assert.AreEqual(1, results.Count);
					Assert.AreEqual("record not found", results[0].text1);
					Assert.AreEqual("empty", results[0].text2);
				}
				else
				{
					var sql = query.ToString();
					Assert.That(sql.Contains("[r].[text1] MATCH 'found'"));
				}
			}
		}

		[Test, Category("FreeText")]
		public void MatchFromTable([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().MatchTable("found");

				var sql = query.ToString();
				Assert.That(sql.Contains("[FTS5_TABLE]('found')"));
			}
		}

		[Test, Category("FreeText")]
		public void RowId([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>().Where(r => r.RowId() == 3);

				if (type != SQLiteFTS.FTS5)
				{
					var results = query.ToList();
					Assert.AreEqual(1, results.Count);
					Assert.AreEqual("record not found", results[0].text1);
					Assert.AreEqual("empty", results[0].text2);
				}
				else
				{
					var sql = query.ToString();
					Assert.That(sql.Contains("[r].[rowid] = 3"));
				}
			}
		}

		[Test, Category("FreeText")]
		public void Rank([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().OrderBy(r => r.Rank());

				var sql = query.ToString();
				Assert.That(sql.Contains("ORDER BY"));
				Assert.That(sql.Contains("[t1].[rank]"));
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Offsets([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>()
					.Where(r => r.Match("found"))
					.OrderBy(r => r.RowId())
					.Select(r => new { r.text1, offsets = r.Fts3Offsets() });

				var results = query.ToList();
				Assert.AreEqual(2, results.Count);
				Assert.AreEqual("looking for something?", results[0].text1);
				Assert.AreEqual("1 0 0 5", results[0].offsets);
				Assert.AreEqual("record not found", results[1].text1);
				Assert.AreEqual("0 0 11 5", results[1].offsets);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3MatchInfo([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>()
					.Where(r => r.Match("found"))
					.OrderBy(r => r.RowId())
					.Select(r => new { r.text1, matchInfo = r.Fts3MatchInfo() });

				var results = query.ToList();
				Assert.AreEqual(2, results.Count);
				Assert.AreEqual("looking for something?", results[0].text1);
				AreEqual(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }, results[0].matchInfo);
				Assert.AreEqual("record not found", results[1].text1);
				AreEqual(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }, results[1].matchInfo);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3MatchInfoWithFormat([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var query = db.GetTable<FtsTable>()
					.Where(r => r.Match("found"))
					.OrderBy(r => r.RowId())
					.Select(r => new { r.text1, matchInfo = r.Fts3MatchInfo("pc") });

				var results = query.ToList();
				Assert.AreEqual(2, results.Count);
				Assert.AreEqual("looking for something?", results[0].text1);
				AreEqual(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, results[0].matchInfo);
				Assert.AreEqual("record not found", results[1].text1);
				AreEqual(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }, results[1].matchInfo);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet1([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("something"))
					.Select(r => r.Fts3Snippet())
					.Single();

				Assert.AreEqual("looking for <b>something</b>?", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet2([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("looking"))
					.Select(r => r.Fts3Snippet("_"))
					.Single();

				Assert.AreEqual("_looking</b> for something?", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet3([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("looking"))
					.Select(r => r.Fts3Snippet("->", "<-"))
					.Single();

				Assert.AreEqual("->looking<- for something?", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet4([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("cool"))
					.Select(r => r.Fts3Snippet(">", "<", "[zzz]"))
					.Single();

				Assert.AreEqual("[zzz]3oC drops. >Cool< in the upper portion, minimum temperature 14-16oC and >cool< elsewhere, minimum[zzz]", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet5([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("cool"))
					.Select(r => r.Fts3Snippet(">", "<", "[zzz]", 0))
					.Single();

				Assert.AreEqual("for snippet testing", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3Snippet6([IncludeDataSources(true, TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(type)))
			{
				var result = db.GetTable<FtsTable>()
					.Where(r => r.Match("cool"))
					.Select(r => r.Fts3Snippet(">", "<", "[zzz]", 1, 1))
					.Single();

				Assert.AreEqual("[zzz]>Cool<[zzz]", result);
			}
		}

		[Test, Category("FreeText")]
		public void Fts5bm25([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().Select(r => r.Fts5bm25());

				var sql = query.ToString();
				Assert.That(sql.Contains("bm25([r].[FTS5_TABLE])"));
			}
		}

		[Test, Category("FreeText")]
		public void Fts5bm25WithWeights([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().Select(r => r.Fts5bm25(1.4, 5.6));

				var sql = query.ToString();
				Assert.That(sql.Contains("bm25([r].[FTS5_TABLE], 1.3999999999999999, 5.5999999999999996)"));
			}
		}

		[Test, Category("FreeText")]
		public void Fts5Highlight([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().Select(r => r.Fts5Highlight(2, "start", "end"));

				var sql = query.ToString();
				Assert.That(sql.Contains("highlight([r].[FTS5_TABLE], 2, 'start', 'end')"));
			}
		}

		[Test, Category("FreeText")]
		public void Fts5Snippet([IncludeDataSources(true, TestProvName.AllSQLite)] string context)
		{
			using (var db = GetDataContext(context, SetupFtsMapping(SQLiteFTS.FTS5)))
			{
				var query = db.GetTable<FtsTable>().Select(r => r.Fts5Snippet(1, "->", "<-", "zzz", 4));

				var sql = query.ToString();
				Assert.That(sql.Contains("snippet([r].[FTS5_TABLE], 1, '->', '<-', 'zzz', 4)"));
			}
		}

		[Test, Category("FreeText")]
		public void Fts3CommandOptimize([IncludeDataSources(TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(type));

				db.Fts3Optimize(db.GetTable<FtsTable>());

				var tableName = type.ToString() + "_TABLE";

				Assert.AreEqual($"INSERT INTO [{tableName}]([{tableName}]) VALUES('optimize')", db.LastQuery);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3CommandRebuild([IncludeDataSources(TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(type));

				db.Fts3Rebuild(db.GetTable<FtsTable>());

				var tableName = type.ToString() + "_TABLE";

				Assert.AreEqual($"INSERT INTO [{tableName}]([{tableName}]) VALUES('rebuild')", db.LastQuery);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3CommandIntegrityCheck([IncludeDataSources(TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(type));

				db.Fts3IntegrityCheck(db.GetTable<FtsTable>());

				var tableName = type.ToString() + "_TABLE";

				Assert.AreEqual($"INSERT INTO [{tableName}]([{tableName}]) VALUES('integrity-check')", db.LastQuery);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3CommandMerge([IncludeDataSources(TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(type));

				db.Fts3Merge(db.GetTable<FtsTable>(), 4, 3);

				var tableName = type.ToString() + "_TABLE";

				Assert.AreEqual($"INSERT INTO [{tableName}]([{tableName}]) VALUES('merge=4,3')", db.LastQuery);
			}
		}

		[Test, Category("FreeText")]
		public void Fts3CommandAutoMerge([IncludeDataSources(TestProvName.AllSQLite)] string context, [Values(SQLiteFTS.FTS3, SQLiteFTS.FTS4)] SQLiteFTS type)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(type));

				db.Fts3AutoMerge(db.GetTable<FtsTable>(), 5);

				var tableName = type.ToString() + "_TABLE";

				Assert.AreEqual($"INSERT INTO [{tableName}]([{tableName}]) VALUES('automerge=5')", db.LastQuery);
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandAutoMerge([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5AutoMerge(db.GetTable<FtsTable>(), 5);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('automerge', 5)", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandCrisisMerge([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5CrisisMerge(db.GetTable<FtsTable>(), 2);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('crisismerge', 2)", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandDelete([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					var record = new FtsTable()
					{
						text1 = "one",
						text2 = "two"
					};

					db.Fts5Delete(db.GetTable<FtsTable>(), 2, record);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rowid, [text1], [text2]) VALUES('delete', 2, @p0, @p1)", db.LastQuery);
					Assert.AreEqual(2, db.Command.Parameters.Count);
					Assert.AreEqual("one", ((DbParameter)db.Command.Parameters[0]).Value);
					Assert.AreEqual("two", ((DbParameter)db.Command.Parameters[1]).Value);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandDeleteAll([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5DeleteAll(db.GetTable<FtsTable>());
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE]) VALUES('delete-all')", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandIntegrityCheck([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5IntegrityCheck(db.GetTable<FtsTable>());
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE]) VALUES('integrity-check')", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandMerge([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5Merge(db.GetTable<FtsTable>(), 234);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('merge', 234)", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandOptimize([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5Optimize(db.GetTable<FtsTable>());
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE]) VALUES('optimize')", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandPgsz([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5Pgsz(db.GetTable<FtsTable>(), 3333);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('pgsz', 3333)", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandRank([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5Rank(db.GetTable<FtsTable>(), "strange('function\")");
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('rank', @rank)", db.LastQuery);
					Assert.AreEqual(1, db.Command.Parameters.Count);
					Assert.AreEqual("strange('function\")", ((DbParameter)db.Command.Parameters[0]).Value);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandRebuild([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5Rebuild(db.GetTable<FtsTable>());
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE]) VALUES('rebuild')", db.LastQuery);
				}
			}
		}

		[Test, Category("FreeText")]
		public void Fts5CommandUserMerge([IncludeDataSources(TestProvName.AllSQLite)] string context)
		{
			using (var db = new TestDataConnection(context))
			{
				db.AddMappingSchema(SetupFtsMapping(SQLiteFTS.FTS5));

				try
				{
					db.Fts5UserMerge(db.GetTable<FtsTable>(), 7);
				}
				catch
				{
					// we don't have FTS5 table, but we need to get sql for validation
				}
				finally
				{
					Assert.AreEqual("INSERT INTO [FTS5_TABLE]([FTS5_TABLE], rank) VALUES('usermerge', 7)", db.LastQuery);
				}
			}
		}
		#endregion
	}

}
