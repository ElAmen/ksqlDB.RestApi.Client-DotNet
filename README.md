This package generates ksql queries from your .NET C# linq queries. You can filter, project, limit, etc. your push notifications server side with [ksqlDB push queries](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-rest-api/streaming-endpoint/).
You can continually process computations over unbounded (theoretically never-ending) streams of data.
It also allows you to execute SQL [statements](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/) via the Rest API such as inserting records into streams and creating tables, types, etc. or executing admin operations such as listing streams.

[ksqlDB.RestApi.Client](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet) is a contribution to [Confluent ksqldb-clients](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-clients/)

[![main](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/actions/workflows/dotnetcore.yml/badge.svg?branch=main&event=push)](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/actions/workflows/dotnetcore.yml/)

Install with NuGet package manager:
```
Install-Package ksqlDB.RestApi.Client
```
or with .NET CLI
```
dotnet add package ksqlDB.RestApi.Client
```
This adds a `<PackageReference>` to your csproj file, similar to the following:
```XML
<PackageReference Include="ksqlDB.RestApi.Client" Version="2.3.0" />
```

Alternative option is to use [Protobuf content type](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/protobuf.md).

The following example can be tried with a [.NET interactive Notebook](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/Notebooks):

```C#
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Options;

var ksqlDbUrl = @"http:\\localhost:8088";

var contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
{
  ShouldPluralizeFromItemName = true
};

await using var context = new KSqlDBContext(contextOptions);

using var disposable = context.CreateQueryStream<Tweet>()
  .WithOffsetResetPolicy(AutoOffsetReset.Latest)
  .Where(p => p.Message != "Hello world" || p.Id == 1)
  .Select(l => new { l.Message, l.Id })
  .Take(2)
  .Subscribe(tweetMessage =>
  {
    Console.WriteLine($"{nameof(Tweet)}: {tweetMessage.Id} - {tweetMessage.Message}");
  }, error => { Console.WriteLine($"Exception: {error.Message}"); }, () => Console.WriteLine("Completed"));

Console.WriteLine("Press any key to stop the subscription");

Console.ReadKey();

public class Tweet : Record
{
  public int Id { get; set; }

  public string Message { get; set; }
}
```

LINQ code written in C# from the sample is equivalent to this ksql query:
```SQL
SELECT Message, Id
  FROM Tweets
 WHERE Message != 'Hello world' OR Id = 1 
  EMIT CHANGES 
 LIMIT 2;
```

In the above mentioned code snippet everything runs server side except of the ```IQbservable<TEntity>.Subscribe``` method. It subscribes to your ksqlDB stream created in the following manner:

```C#
using ksqlDB.RestApi.Client.KSql.RestApi.Http;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.Api.Client.Samples.Models;

EntityCreationMetadata metadata = new()
{
  KafkaTopic = nameof(Tweet),
  Partitions = 1,
  Replicas = 1
};

var httpClient = new HttpClient()
{
  BaseAddress = new Uri(@"http:\\localhost:8088")
};

var httpClientFactory = new HttpClientFactory(httpClient);
var restApiClient = new KSqlDbRestApiClient(httpClientFactory);

var httpResponseMessage = await restApiClient.CreateOrReplaceStreamAsync<Tweet>(metadata);
```

CreateOrReplaceStreamAsync executes the following statement:
```SQL
CREATE OR REPLACE STREAM Tweets (
	Id INT,
	Message VARCHAR
) WITH ( KAFKA_TOPIC='Tweet', VALUE_FORMAT='Json', PARTITIONS='1', REPLICAS='1' );
```

Run the following insert statements to stream some messages with your ksqldb-cli
```
docker exec -it $(docker ps -q -f name=ksqldb-cli) ksql http://ksqldb-server:8088
```
```SQL
INSERT INTO tweets (id, message) VALUES (1, 'Hello world');
INSERT INTO tweets (id, message) VALUES (2, 'ksqlDB rulez!');
```

or insert a record from C#:
```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

var responseMessage = await new KSqlDbRestApiClient(httpClientFactory)
  .InsertIntoAsync(new Tweet { Id = 2, Message = "ksqlDB rulez!" });
```

or with KSqlDbContext:

```C#
await using var context = new KSqlDBContext(ksqlDbUrl);

context.Add(new Tweet { Id = 1, Message = "Hello world" });
context.Add(new Tweet { Id = 2, Message = "ksqlDB rulez!" });

var saveChangesResponse = await context.SaveChangesAsync();
```

Sample project can be found under [Samples](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/ksqlDB.RestApi.Client.Sample) solution folder in ksqlDB.RestApi.Client.sln 


**External dependencies:**
- [kafka broker](https://kafka.apache.org/intro) and [ksqlDB-server](https://ksqldb.io/overview.html) 0.14.0
- the solution requires [Docker desktop](https://www.docker.com/products/docker-desktop) and Visual Studio 2019
- [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

Clone the repository
```
git clone https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet.git
```

CD to [Samples](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/ksqlDB.RestApi.Client.Sample)
```
CD Samples\ksqlDB.RestApi.Client.Sample\
```

run in command line:

```docker compose up -d```

**AspNet Blazor server side sample:**

- set docker-compose.csproj as startup project in InsideOut.sln for an embedded Kafka connect integration and stream processing examples.

# Kafka stream processing example
Example of how to consume a table with a kafka consumer. The following code is based on sample named [InsideOut](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/InsideOut)
```
Install-Package ksqlDB.RestApi.Client
```

```C#
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq.Statements;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.RestApi.Extensions;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations;

private async Task CreateOrReplaceMaterializedTableAsync()
{
  string ksqlDbUrl = "http://localhost:8088";

  await using var context = new KSqlDBContext(ksqlDbUrl);

  var statement = context.CreateOrReplaceTableStatement(tableName: "SENSORSTABLE")
    .As<IoTSensor>("IotSensors")
    .Where(c => c.SensorId != "Sensor-5")
    .GroupBy(c => c.SensorId)
    .Select(c => new { SensorId = c.Key, Count = c.Count(), AvgValue = c.Avg(a => a.Value) });

  var httpResponseMessage = await statement.ExecuteStatementAsync();

  if (!httpResponseMessage.IsSuccessStatusCode)
  {
    var statementResponse = httpResponseMessage.ToStatementResponse();
  }
}

public record IoTSensor
{
  [Key]
  public string SensorId { get; set; }
  public int Value { get; set; }
}
```

```C#
public class SensorsTableConsumer : KafkaConsumer<string, IoTSensorStats>
{
  public SensorsTableConsumer(ConsumerConfig consumerConfig)
    : base("SENSORSTABLE", consumerConfig)
  {
  }
}

public record IoTSensorStats
{
  public string SensorId { get; set; }
  public double AvgValue { get; set; }
  public int Count { get; set; }
}
```

```
Install-Package System.Interactive.Async -Version 5.0.0
```

```C#
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Confluent.Kafka;
using InsideOut.Consumer;

const string bootstrapServers = "localhost:29092";

static async Task Main(string[] args)
{
  var consumerConfig = new ConsumerConfig
                       {
                         BootstrapServers = bootstrapServers,
                         GroupId = "Client-01",
                         AutoOffsetReset = AutoOffsetReset.Latest
                       };

  var kafkaConsumer = new KafkaConsumer<string, IoTSensorStats>("IoTSensors", consumerConfig);

  await foreach (var consumeResult in kafkaConsumer.ConnectToTopic().ToAsyncEnumerable().Take(10))
  {
    Console.WriteLine(consumeResult.Message);
  }

  using (kafkaConsumer)
  { }
}
```

[Blazor server side example](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet) - InsideOut.sln

# Setting query parameters (v0.1.0)
Default settings:
'auto.offset.reset' is set to 'earliest' by default. 
New parameters could be added or existing ones changed in the following manner:
```C#
var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088");

contextOptions.QueryStreamParameters["auto.offset.reset"] = "latest";
```

### Record (row) class (v0.1.0)
Record class is a base class for rows returned in push queries. It has a 'RowTime' property.

```C#
public class Tweet : ksqlDB.RestApi.Client.KSql.Query.Record
{
  public string Message { get; set; }
}

context.CreateQueryStream<Tweet>()
  .Select(c => new { c.RowTime, c.Message });
```

### Overriding stream names (v0.1.0)
Stream names are generated based on the generic record types. They are pluralized with Pluralize.NET package

**By default the generated from item names such as stream and table names are pluralized**. This behaviour could be switched off with the following `ShouldPluralizeStreamName` configuration. 
> ⚠  KSqlDBContextOptions.ShouldPluralizeStreamName was renamed to ShouldPluralizeFromItemName

```C#
context.CreateQueryStream<Person>();
```
```SQL
FROM People
```
This can be disabled:
```C#
var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088")
{
  ShouldPluralizeStreamName = false
};

new KSqlDBContext(contextOptions).CreateQueryStream<Person>();
```
```SQL
FROM Person
```

In v1.0 was ShouldPluralizeStreamName renamed to **ShouldPluralizeFromItemName**
```C#
var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088")
{
  ShouldPluralizeFromItemName = false
};
```

Setting an arbitrary stream name (from_item name):
```C#
context.CreateQueryStream<Tweet>("custom_topic_name");
```
```SQL
FROM custom_topic_name
```

# ```IQbservable<T>``` extension methods
<img src="https://www.codeproject.com/KB/cs/646361/WhatHowWhere.jpg" />

List of supported [push query](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/push_queries.md) extension methods:
- [Take (LIMIT)](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/push_queries.md#take-limit-v010)
- [Subscribe](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/push_queries.md#subscribe-v010)
- [ToObservable](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/push_queries.md#toobservable-v010)

### Select (v0.1.0)
Projects each element of a stream into a new form.
```C#
context.CreateQueryStream<Tweet>()
  .Select(l => new { l.RowTime, l.Message });
```
Omitting select is equivalent to SELECT *
### Supported data types mapping

|     ksql     |            c#            |
|:------------:|:------------------------:|
|    VARCHAR   |          string          |
|    INTEGER   |            int           |
|    BIGINT    |           long           |
|    DOUBLE    |          double          |
|    BOOLEAN   |           bool           |
|     BYTES    |         byte[] **        |
|  ```ARRAY``` | C#Type[] or IEnumerable* |
|   ```MAP```  |        IDictionary       |
| ```STRUCT``` |          struct          |
|     DATE     |     System.DateTime      |
|     TIME     |     System.TimeSpan      |
|   TIMESTAMP  |    System.DateTimeOffset |

\* IEnumerable was added in version 1.6.0

\** Bytes were added in version 1.9.0 (ksqldb 0.21.0)

Array type mapping example (available from v0.3.0):
All of the elements in the array must be of the same type. The element type can be any valid SQL type.
```
ksql: ARRAY<INTEGER>
C#  : int[]
```
Destructuring an array (ksqldb represents the first element of an array as 1):
```C#
queryStream
  .Select(_ => new { FirstItem = new[] {1, 2, 3}[1] })
```
Generates the following KSQL:
```KSQL
ARRAY[1, 2, 3][1] AS FirstItem
```
Array length:
```C#
queryStream
  .Select(_ => new[] {1, 2, 3}.Length)
```
Generates the following KSQL:
```KSQL
ARRAY_LENGTH(ARRAY[1, 2, 3])
```

Struct type mapping example (available from v0.5.0):
A struct represents strongly typed structured data. A struct is an ordered collection of named fields that have a specific type. The field types can be any valid SQL type.
```C#
struct Point
{
  public int X { get; set; }

  public int Y { get; set; }
}

queryStream
  .Select(c => new Point { X = c.X, Y = 2 });
```
Generates the following KSQL:
```KSQL
SELECT STRUCT(X := X, Y := 2) FROM StreamName EMIT CHANGES;
```

Destructure a struct:
```C#
queryStream
  .Select(c => new Point { X = c.X, Y = 2 }.X);
```
```KSQL
SELECT STRUCT(X := X, Y := 2)->X FROM StreamName EMIT CHANGES;
```

### Where (v0.1.0)
Filters a stream of values based on a predicate.
```C#
context.CreateQueryStream<Tweet>()
  .Where(p => p.Message != "Hello world" || p.Id == 1)
  .Where(p => p.RowTime >= 1510923225000);
```
Multiple Where statements are joined with AND operator. 
```KSQL
SELECT * FROM Tweets
WHERE Message != 'Hello world' OR Id = 1 AND RowTime >= 1510923225000
EMIT CHANGES;
```

Supported operators are:

|   ksql   |           meaning           |  c#  |
|:--------:|:---------------------------:|:----:|
| =        | is equal to                 | ==   |
| != or <> | is not equal to             | !=   |
| <        | is less than                | <    |
| <=       | is less than or equal to    | <=   |
| >        | is greater than             | >    |
| >=       | is greater than or equal to | >=   |
| AND      | logical AND                 | &&   |
| OR       | logical OR                  | \|\| |
| NOT      | logical NOT                 |  !   |


### ToQueryString (v0.1.0)
ToQueryString is helpful for debugging purposes. It returns the generated ksql query without executing it.
```C#
var ksql = context.CreateQueryStream<Tweet>().ToQueryString();

//prints SELECT * FROM tweets EMIT CHANGES;
Console.WriteLine(ksql);
```

### Aggregation functions
List of supported ksqldb [aggregation functions](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md):
- [MIN, MAX](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#min-and-max-v020)
- [AVG](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#avg-v020)
- [COUNT](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#count-v010)
- [COLLECT_LIST, COLLECT_SET, EARLIEST_BY_OFFSET, LATEST_BY_OFFSET](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#collect_list-collect_set-earliest_by_offset-latest_by_offset)
- [SUM](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#sum)
- COUNT_DISTINCT
- HISTOGRAM
- [TOPK,TOPKDISTINCT](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#topk-topkdistinct-longcount-countcolumn-v030)

- [TimeWindows - EMIT FINAL](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#timewindows---emit-final-v250)

[Rest api reference](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/aggregate-functions/)

### GroupBy (v0.1.0)
Extract records from an aggregation that fulfill a specified condition with the [HAVING](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/aggregations.md#having-v020) keyword.

#### Count (v0.1.0)
Count the number of rows. When * is specified, the count returned will be the total number of rows.
```C#
var ksqlDbUrl = @"http:\\localhost:8088";
var contextOptions = new KSqlDBContextOptions(ksqlDbUrl);
var context = new KSqlDBContext(contextOptions);

context.CreateQueryStream<Tweet>()
  .GroupBy(c => c.Id)
  .Having(c => c.Count() > 2)
  .Select(g => new { Id = g.Key, Count = g.Count() })
  .Subscribe(count =>
  {
    Console.WriteLine($"{count.Id} Count: {count.Count}");
    Console.WriteLine();
  }, error => { Console.WriteLine($"Exception: {error.Message}"); }, () => Console.WriteLine("Completed"));
```
```SQL
SELECT Id, COUNT(*) Count FROM Tweets GROUP BY Id EMIT CHANGES;
```

> ⚠ There is a known limitation in the early access versions (bellow version 1.10). The aggregation functions have to be named/aliased COUNT(*) Count, otherwise the deserialization won't be able to map the unknown column name KSQL_COL_0. 
The Key should be mapped back to the respective column too Id = g.Key. See IKSqlGrouping.Source (v1.10.0).

Or without the new expression:
```C#
context.CreateQueryStream<Tweet>()
  .GroupBy(c => c.Id)
  .Select(g => g.Count()); 
```
```SQL
SELECT COUNT(*) FROM Tweets GROUP BY Id EMIT CHANGES;
```

### ToAsyncEnumerable (v0.1.0)
Creates an [async iterator](https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8) from the query:
```C#
var cts = new CancellationTokenSource();
var asyncTweetsEnumerable = context.CreateQueryStream<Tweet>().ToAsyncEnumerable();

await foreach (var tweet in asyncTweetsEnumerable.WithCancellation(cts.Token))
  Console.WriteLine(tweet.Message);
```

### WindowedBy (v0.1.0)
Creation of windowed aggregation

[Tumbling window](https://docs.ksqldb.io/en/latest/concepts/time-and-windows-in-ksqldb-queries/#tumbling-window):
```C#
var context = new TransactionsDbProvider(ksqlDbUrl);

var windowedQuery = context.CreateQueryStream<Transaction>()
  .WindowedBy(new TimeWindows(Duration.OfSeconds(5)).WithGracePeriod(Duration.OfHours(2)))
  .GroupBy(c => c.CardNumber)
  .Select(g => new { CardNumber = g.Key, Count = g.Count() });
```

```KSQL
SELECT CardNumber, COUNT(*) Count FROM Transactions 
  WINDOW TUMBLING (SIZE 5 SECONDS, GRACE PERIOD 2 HOURS) 
  GROUP BY CardNumber EMIT CHANGES;
```

[Hopping window](https://docs.ksqldb.io/en/latest/concepts/time-and-windows-in-ksqldb-queries/#hopping-window):
```C#
var subscription = context.CreateQueryStream<Tweet>()
  .GroupBy(c => c.Id)
  .WindowedBy(new HoppingWindows(Duration.OfSeconds(5)).WithAdvanceBy(Duration.OfSeconds(4)).WithRetention(Duration.OfDays(7)))
  .Select(g => new { g.WindowStart, g.WindowEnd, Id = g.Key, Count = g.Count() })
  .Subscribe(c => { Console.WriteLine($"{c.Id}: {c.Count}: {c.WindowStart}: {c.WindowEnd}"); }, exception => {});
```

```KSQL
SELECT WindowStart, WindowEnd, Id, COUNT(*) Count FROM Tweets 
  WINDOW HOPPING (SIZE 5 SECONDS, ADVANCE BY 10 SECONDS, RETENTION 7 DAYS) 
  GROUP BY Id EMIT CHANGES;
```
Window advancement interval should be more than zero and less than window duration

### String Functions UCase, LCase (v0.1.0)
```C#
l => l.Message.ToLower() != "hi";
l => l.Message.ToUpper() != "HI";
```
```KSQL
LCASE(Latitude) != 'hi'
UCASE(Latitude) != 'HI'
```

# v0.2.0

### Session Window (v0.2.0)
A [session window](https://docs.ksqldb.io/en/latest/concepts/time-and-windows-in-ksqldb-queries/#session-window) aggregates records into a session, which represents a period of activity separated by a specified gap of inactivity, or "idleness". 
```C#
var query = context.CreateQueryStream<Transaction>()
  .GroupBy(c => c.CardNumber)
  .WindowedBy(new SessionWindow(Duration.OfSeconds(5)))
  .Select(g => new { CardNumber = g.Key, Count = g.Count() });
```
KSQL:
```KSQL
SELECT CardNumber, COUNT(*) Count FROM Transactions 
  WINDOW SESSION (5 SECONDS)
  GROUP BY CardNumber 
  EMIT CHANGES;
```
Time units:
```C#
using ksqlDB.RestApi.Client.KSql.Query.Windows;

public enum TimeUnits
{
  MILLISECONDS, // v2.0.0
  SECONDS,
  MINUTES,
  HOURS,
  DAYS
}

Duration duration = Duration.OfHours(2);

Console.WriteLine($"{duration.Value} {duration.TimeUnit}");
```

### Inner Joins (v0.2.0)
How to [join table and table](https://kafka-tutorials.confluent.io/join-a-table-to-a-table/ksql.html)
```C#
public class Movie : Record
{
  public string Title { get; set; }
  public int Id { get; set; }
  public int Release_Year { get; set; }
}

public class Lead_Actor : Record
{
  public string Title { get; set; }
  public string Actor_Name { get; set; }
}

using ksqlDB.RestApi.Client.KSql.Linq;

var query = context.CreateQueryStream<Movie>()
  .Join(
    Source.Of<Lead_Actor>(nameof(Lead_Actor)),
    movie => movie.Title,
    actor => actor.Title,
    (movie, actor) => new
    {
      movie.Id,
      Title = movie.Title,
      movie.Release_Year,
      ActorName = K.Functions.RPad(K.Functions.LPad(actor.Actor_Name.ToUpper(), 15, "*"), 25, "^"),
      ActorTitle = actor.Title
    }
  );

var joinQueryString = query.ToQueryString();
```
KSQL:
```KSQL
SELECT M.Id Id, M.Title Title, M.Release_Year Release_Year, RPAD(LPAD(UCASE(L.Actor_Name), 15, '*'), 25, '^') ActorName, L.Title ActorTitle 
FROM Movies M
INNER JOIN Lead_Actor L
ON M.Title = L.Title
EMIT CHANGES;
```

> ⚠ There is a known limitation in the early access versions (bellow 1.0). 
The Key column, in this case movie.Title, has to be aliased Title = movie.Title, otherwise the deserialization won't be able to map the unknown column name M_TITLE. 


### Like (v0.2.0)
```C#
using ksqlDB.RestApi.Client.KSql.Query.Functions;

Expression<Func<Tweet, bool>> likeExpression = c => KSql.Functions.Like(c.Message, "%santa%");

Expression<Func<Tweet, bool>> likeLCaseExpression = c => KSql.Functions.Like(c.Message.ToLower(), "%santa%".ToLower());
```
KSQL
```KSQL
"LCASE(Message) LIKE LCASE('%santa%')"
```

### Arithmetic operations on columns (v0.2.0)
The usual arithmetic operators (+,-,/,*,%) may be applied to numeric types, like INT, BIGINT, and DOUBLE:
```KSQL
SELECT USERID, LEN(FIRST_NAME) + LEN(LAST_NAME) AS NAME_LENGTH FROM USERS EMIT CHANGES;
```
```C#
Expression<Func<Person, object>> expression = c => c.FirstName.Length * c.LastName.Length;
```

### String function - Length (LEN) (v0.2.0)
```C#
Expression<Func<Tweet, int>> lengthExpression = c => c.Message.Length;
```
KSQL
```KSQL
LEN(Message)
```

# v0.3.0

### LeftJoin - LEFT OUTER (v0.3.0)
LEFT OUTER joins will contain leftRecord-NULL records in the result stream, which means that the join contains NULL values for fields selected from the right-hand stream where no match is made.
```C#
var query = new KSqlDBContext(@"http:\\localhost:8088").CreateQueryStream<Movie>()
  .LeftJoin(
    Source.Of<Lead_Actor>(),
    movie => movie.Title,
    actor => actor.Title,
    (movie, actor) => new
    {
      movie.Id,
      ActorTitle = actor.Title
    }
  );
```
Generated KSQL:
```KSQL
SELECT M.Id Id, L.Title ActorTitle FROM Movies M
LEFT JOIN Lead_Actors L
ON M.Title = L.Title
EMIT CHANGES;
```

### Having - aggregations with column (v0.3.0)
[Example](https://kafka-tutorials.confluent.io/finding-distinct-events/ksql.html) shows how to use Having with Count(column) and Group By compound key:
```C#
public class Click
{
  public string IP_ADDRESS { get; set; }
  public string URL { get; set; }
  public string TIMESTAMP { get; set; }
}

var query = context.CreateQueryStream<Click>()
  .GroupBy(c => new { c.IP_ADDRESS, c.URL, c.TIMESTAMP })
  .WindowedBy(new TimeWindows(Duration.OfMinutes(2)))
  .Having(c => c.Count(g => c.Key.IP_ADDRESS) == 1)
  .Select(g => new { g.Key.IP_ADDRESS, g.Key.URL, g.Key.TIMESTAMP })
  .Take(3);
```
Generated KSQL:
```KSQL
SELECT IP_ADDRESS, URL, TIMESTAMP FROM Clicks WINDOW TUMBLING (SIZE 2 MINUTES) GROUP BY IP_ADDRESS, URL, TIMESTAMP 
HAVING COUNT(IP_ADDRESS) = 1 EMIT CHANGES LIMIT 3;
```

### Where IS NULL, IS NOT NULL (v0.3.0)
```C#
using var subscription = new KSqlDBContext(@"http:\\localhost:8088")
  .CreateQueryStream<Click>()
  .Where(c => c.IP_ADDRESS != null || c.IP_ADDRESS == null)
  .Select(c => new { c.IP_ADDRESS, c.URL, c.TIMESTAMP });
```

Generated KSQL:
```KSQL
SELECT IP_ADDRESS, URL, TIMESTAMP
FROM Clicks
WHERE IP_ADDRESS IS NOT NULL OR IP_ADDRESS IS NULL
EMIT CHANGES;
```

### Dynamic - calling not supported ksqldb functions (v0.3.0)
Some of the ksqldb functions have not been implemented yet. This can be circumvented by calling K.Functions.Dynamic with the appropriate function call and its parameters. The type of the column value is set with C# **as** operator.
```C#
using ksqlDB.RestApi.Client.KSql.Query.Functions;

context.CreateQueryStream<Tweet>()
  .Select(c => new { Col = KSql.Functions.Dynamic("IFNULL(Message, 'n/a')") as string, c.Id, c.Amount, c.Message });
```
The interesting part from the above query is:
```C#
K.Functions.Dynamic("IFNULL(Message, 'n/a')") as string
```
Generated KSQL:
```KSQL
SELECT IFNULL(Message, 'n/a') Col, Id, Amount, Message FROM Tweets EMIT CHANGES;
```
Result:
```
+----------------------------+----------------------------+----------------------------+----------------------------+
|COL                         |ID                          |AMOUNT                      |MESSAGE                     |
+----------------------------+----------------------------+----------------------------+----------------------------+
|Hello world                 |1                           |0.0031                      |Hello world                 |
|n/a                         |1                           |0.1                         |null                        |
```

Dynamic function call with array result example:
```C#
using K = ksqlDB.RestApi.Client.KSql.Query.Functions.KSql;

context.CreateQueryStream<Tweet>()
  .Select(c => K.F.Dynamic("ARRAY_DISTINCT(ARRAY[1, 1, 2, 3, 1, 2])") as int[])
  .Subscribe(
    message => Console.WriteLine($"{message[0]} - {message[^1]}"), 
    error => Console.WriteLine($"Exception: {error.Message}"));
```

### Aggregation functions: CollectSet, CollectList, CountDistinct (v0.3.0)
```C#
var subscription = context.CreateQueryStream<Tweet>()
  .GroupBy(c => c.Id)
  .Select(g => new { Id = g.Key, Array = g.CollectSet(c => c.Message) })
  //.Select(g => new { Id = g.Key, Array = g.CollectList(c => c.Message) })
  .Subscribe(c =>
  {
    Console.WriteLine($"{c.Id}:");
    foreach (var value in c.Array)
    {
      Console.WriteLine($"  {value}");
    }
  }, exception => { Console.WriteLine(exception.Message); });
```
Generated KSQL:
```KSQL
SELECT Id, COLLECT_SET(Message) Array 
FROM Tweets GROUP BY Id EMIT CHANGES;

SELECT Id, COLLECT_LIST(Message) Array 
FROM Tweets GROUP BY Id EMIT CHANGES;
```

CountDistinct, LongCountDistinct
```C#
var subscription = context.CreateQueryStream<Tweet>()
  .GroupBy(c => c.Id)
  // .Select(g => new { Id = g.Key, Count = g.CountDistinct(c => c.Message) })
  .Select(g => new { Id = g.Key, Count = g.LongCountDistinct(c => c.Message) })
  .Subscribe(c =>
  {
    Console.WriteLine($"{c.Id} - {c.Count}");
  }, exception => { Console.WriteLine(exception.Message); });
```
Generated KSQL:
```KSQL
SELECT Id, COUNT_DISTINCT(Message) Count 
FROM Tweets GROUP BY Id EMIT CHANGES;
```

# v0.4.0

### Maps (v0.4.0)
[Maps](https://docs.ksqldb.io/en/latest/how-to-guides/query-structured-data/#maps)
are an associative data type that map keys of any type to values of any type. The types across all keys must be the same. The same rule holds for values. Destructure maps using bracket syntax ([]).
```C#
var dictionary = new Dictionary<string, int>()
{
  { "c", 2 },
  { "d", 4 }
};
``` 
```KSQL
MAP('c' := 2, 'd' := 4)
```

Accessing map elements:
```C#
dictionary["c"]
``` 
```KSQL
MAP('c' := 2, 'd' := 4)['d'] 
```
Deeply nested types:
```C#
context.CreateQueryStream<Tweet>()
  .Select(c => new
  {
    Map = new Dictionary<string, int[]>
    {
      { "a", new[] { 1, 2 } },
      { "b", new[] { 3, 4 } },
    }
  });
```
Generated KSQL:
```KSQL
SELECT MAP('a' := ARRAY[1, 2], 'b' := ARRAY[3, 4]) Map 
FROM Tweets EMIT CHANGES;
```

# v0.4.0
[Some KSql function examples can be found here](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/wiki/KSql-functions)

# v0.5.0

### Structs (v0.5.0)
[Structs](https://docs.ksqldb.io/en/latest/how-to-guides/query-structured-data/#structs)
 are an associative data type that map VARCHAR keys to values of any type. Destructure structs by using arrow syntax (->).
```C#
public struct Point
{
  public int X { get; set; }

  public int Y { get; set; }
}
```

```C#
query
  .Select(c => new Point { X = 1, Y = 2 });
```

```SQL
SELECT STRUCT(X := 1, Y := 2) FROM point EMIT CHANGES;
```

### Full Outer Join (v0.5.0)
FULL OUTER joins will contain leftRecord-NULL or NULL-rightRecord records in the result stream, which means that the join contains NULL values for fields coming from a stream where no match is made.
Define nullable primitive value types in POCOs:
```C#
public record Movie
{
  public long RowTime { get; set; }
  public string Title { get; set; }
  public int? Id { get; set; }
  public int? Release_Year { get; set; }
}

public class Lead_Actor
{
  public string Title { get; set; }
  public string Actor_Name { get; set; }
}
```

```C#
var source = new KSqlDBContext(@"http:\\localhost:8088")
  .CreateQueryStream<Movie>()
  .FullOuterJoin(
    Source.Of<Lead_Actor>("Actors"),
    movie => movie.Title,
    actor => actor.Title,
    (movie, actor) => new
    {
      movie.Id,
      Title = movie.Title,
      movie.Release_Year,
      ActorTitle = actor.Title
    }
  );
```

Generated KSQL:
```KSQL
SELECT m.Id Id, m.Title Title, m.Release_Year Release_Year, l.Title ActorTitle FROM movies_test m
FULL OUTER JOIN lead_actor_test l
ON m.Title = l.Title
EMIT CHANGES;
```

# v0.6.0:
### CASE (v0.6.0)
- Select a condition from one or more expressions.
```C#
var query = new KSqlDBContext(@"http:\\localhost:8088")
  .CreateQueryStream<Tweet>()
  .Select(c =>
    new
    {
      case_result =
        (c.Amount < 2.0) ? "small" :
        (c.Amount < 4.1) ? "medium" : "large"
    }
  );
```

```KSQL
SELECT 
  CASE 
    WHEN Amount < 2 THEN 'small' 
    WHEN Amount < 4.1 THEN 'medium' 
    ELSE 'large' 
  END AS case_result 
FROM Tweets EMIT CHANGES;
```

**NOTE:** Switch expressions and if-elseif-else statements are not supported at current versions

### KSqlDbContextOptionsBuilder (v0.6.0)
> ⚠ KSqlDBContextOptions created with a constructor or by KSqlDbContextOptionsBuilder sets auto.offset.reset to earliest by default.
> This was changed in version 2.0.0

```C#
public static KSqlDBContextOptions CreateQueryStreamOptions(string ksqlDbUrl)
{
  var contextOptions = new KSqlDbContextOptionsBuilder()
    .UseKSqlDb(ksqlDbUrl)
    .SetupQueryStream(options =>
    {
    })
    .SetupQuery(options =>
    {
      options.Properties[QueryParameters.AutoOffsetResetPropertyName] = AutoOffsetReset.Latest.ToString().ToLower();
    })
    .Options;

  return contextOptions;
}
```

# TFM netstandard 2.0 (.Net Framework, NetCoreApp 2.0 etc.) (v0.6.0)
netstandard 2.0 does not support Http 2.0. Due to this ```IKSqlDBContext.CreateQueryStream<TEntity>``` is not exposed at the current version. 
For these reasons ```IKSqlDBContext.CreateQuery<TEntity>``` was introduced to provide the same functionality via Http 1.1. 

### CreateQueryStream (v0.1.0)
[Executing pull or push queries](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-rest-api/streaming-endpoint/#executing-pull-or-push-queries)
```JSON
POST /query-stream HTTP/2.0
Accept: application/vnd.ksqlapi.delimited.v1
Content-Type: application/vnd.ksqlapi.delimited.v1

{
  "sql": "SELECT * FROM movies EMIT CHANGES;",
  "properties": {
    "auto.offset.reset": "earliest"
  }
}
```
```C#
using System;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.Sample.Models.Movies;

var ksqlDbUrl = @"http:\\localhost:8088";
var contextOptions = CreateQueryStreamOptions(ksqlDbUrl);

await using var context = new KSqlDBContext(contextOptions);

using var disposable = context.CreateQueryStream<Movie>()        
  .Subscribe(onNext: movie =>
  {
    Console.WriteLine($"{nameof(Movie)}: {movie.Id} - {movie.Title} - {movie.RowTime}");
    Console.WriteLine();
  }, onError: error => { Console.WriteLine($"Exception: {error.Message}"); }, onCompleted: () => Console.WriteLine("Completed"));
```

### CreateQuery (v0.6.0)
[Run a query](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-rest-api/query-endpoint/#post-query)
```JSON
POST /query HTTP/1.1
Accept: application/vnd.ksql.v1+json
Content-Type: application/vnd.ksql.v1+json

{
  "ksql": "SELECT * FROM movies EMIT CHANGES;",
  "streamsProperties": {
    "ksql.streams.auto.offset.reset": "earliest"
  }
}
```
```C#
using System;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.Sample.Models.Movies;

var ksqlDbUrl = @"http:\\localhost:8088";
var contextOptions = CreateQueryStreamOptions(ksqlDbUrl);

await using var context = new KSqlDBContext(contextOptions);

using var disposable = context.CreateQuery<Movie>()        
  .Subscribe(onNext: movie =>
  {
    Console.WriteLine($"{nameof(Movie)}: {movie.Id} - {movie.Title} - {movie.RowTime}");
    Console.WriteLine();
  }, onError: error => { Console.WriteLine($"Exception: {error.Message}"); }, onCompleted: () => Console.WriteLine("Completed"));
```

# v0.7.0:
- scalar collection functions: ArrayIntersect, ArrayJoin

### Lexical precedence (v0.7.0)
You can use parentheses to change the order of evaluation:
```C#
await using var context = new KSqlDBContext(@"http:\\localhost:8088");

var query = context.CreateQueryStream<Location>()
  .Select(c => (c.Longitude + c.Longitude) * c.Longitude);
```

```KSQL
SELECT (Longitude + Longitude) * Longitude FROM Locations EMIT CHANGES;
```

In Where clauses:
```C#
await using var context = new KSqlDBContext(@"http:\\localhost:8088");

var query = context.CreateQueryStream<Location>()
  .Where(c => (c.Latitude == "1" || c.Latitude != "2") && c.Latitude == "3");
```

```KSQL
SELECT * FROM Locations
WHERE ((Latitude = '1') OR (Latitude != '2')) AND (Latitude = '3') EMIT CHANGES;
```

Redundant brackets are not reduced in the current version

### Raw string KSQL query execution (v0.7.0)
The following examples show how to execute ksql queries from strings:
```C#
string ksql = @"SELECT * FROM Movies
WHERE Title != 'E.T.' EMIT CHANGES LIMIT 2;";

QueryParameters queryParameters = new QueryParameters
{
  Sql = ksql,
  [QueryParameters.AutoOffsetResetPropertyName] = "earliest",
};

await using var context = new KSqlDBContext(@"http:\\localhost:8088");

var moviesSource = context.CreateQuery<Movie>(queryParameters)
  .ToObservable();
```

Query stream:
```C#
string ksql = @"SELECT * FROM Movies
WHERE Title != 'E.T.' EMIT CHANGES LIMIT 2;";

QueryStreamParameters queryStreamParameters = new QueryStreamParameters
{
  Sql = ksql,
  [QueryStreamParameters.AutoOffsetResetPropertyName] = "earliest",
};

await using var context = new KSqlDBContext(@"http:\\localhost:8088");

var source = context.CreateQueryStream<Movie>(queryStreamParameters)
  .ToObservable();
```

# KSqlDbRestApiClient (v0.8.0)
### ExecuteStatementAsync (v0.8.0)
[Execute a statement](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-rest-api/ksql-endpoint/) - The /ksql resource runs a sequence of SQL statements. All statements, except those starting with SELECT, can be run on this endpoint. To run SELECT statements use the /query endpoint.

```C#
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;

public async Task ExecuteStatementAsync()
{
  var ksqlDbUrl = @"http:\\localhost:8088";

  var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

  IKSqlDbRestApiClient restApiClient = new KSqlDbRestApiClient(httpClientFactory);

  var statement = $@"CREATE OR REPLACE TABLE {nameof(Movies)} (
        title VARCHAR PRIMARY KEY,
        id INT,
        release_year INT
      ) WITH (
        KAFKA_TOPIC='{nameof(Movies)}',
        PARTITIONS=1,
        VALUE_FORMAT = 'JSON'
      );";

  KSqlDbStatement ksqlDbStatement = new(statement);
  var httpResponseMessage = await restApiClient.ExecuteStatementAsync(ksqlDbStatement);

  string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
}

public record Movies
{
  public int Id { get; set; }

  public string Title { get; set; }

  public int Release_Year { get; set; }
}
```

### KSqlDbStatement (v0.8.0)
KSqlDbStatement allows you to set the statement, content encoding and [CommandSequenceNumber](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-rest-api/ksql-endpoint/#coordinate-multiple-requests). 

```C#
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;

public KSqlDbStatement CreateStatement(string statement)
{
  KSqlDbStatement ksqlDbStatement = new(statement) {
    ContentEncoding = Encoding.Unicode,
    CommandSequenceNumber = 10,
    [QueryStreamParameters.AutoOffsetResetPropertyName] = "earliest",
  };
	
  return ksqlDbStatement;
}
```

### HttpResponseMessage ToStatementResponses extension (v0.8.0)
```C#
using ksqlDB.RestApi.Client.KSql.RestApi.Extensions;

var httpResponseMessage = await restApiClient.ExecuteStatementAsync(ksqlDbStatement);

var responses = httpResponseMessage.ToStatementResponses();

foreach (var response in responses)
{
	Console.WriteLine(response.CommandStatus);
	Console.WriteLine(response.CommandId);
}
```

# v0.9.0:

# CreateOrReplaceTableStatement (v.0.9.0)

| Statement                                                                                                             | Description                                                                                                                                                                                     |
|-----------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [EXECUTE STATEMENTS](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/)                              | CreateStatementAsync - execution of general-purpose string statements                                                                                                                           |
| [CREATE STREAM](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-stream/)                     | CreateStreamAsync - Create a new stream with the specified columns and properties.                                                                                                              |
| [CREATE TABLE](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-table/)                       | CreateTableAsync - Create a new table with the specified columns and properties.                                                                                                                |
| [CREATE STREAM AS SELECT](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-stream-as-select/) | CreateOrReplaceStreamStatement - Create or replace a new materialized stream view, along with the corresponding Kafka topic, and stream the result of the query into the topic.                 |
| [CREATE TABLE AS SELECT](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-table-as-select/)   | CreateOrReplaceTableStatement - Create or replace a ksqlDB materialized table view, along with the corresponding Kafka topic, and stream the result of the query as a changelog into the topic. |

```C#
using ksqlDB.RestApi.Client.KSql.Linq.Statements;
using ksqlDB.RestApi.Client.KSql.Query.Context;

public static async Task Main(string[] args)
{
  await using var context = new KSqlDBContext(@"http:\\localhost:8088");
  await CreateOrReplaceTableStatement(context);
}

private static async Task CreateOrReplaceTableStatement(IKSqlDBStatementsContext context)
{
  var creationMetadata = new CreationMetadata
  {
    KafkaTopic = "moviesByTitle",		
    KeyFormat = SerializationFormats.Json,
    ValueFormat = SerializationFormats.Json,
    Replicas = 1,
    Partitions = 1
  };

  var httpResponseMessage = await context.CreateOrReplaceTableStatement(tableName: "MoviesByTitle")
    .With(creationMetadata)
    .As<Movie>()
    .Where(c => c.Id < 3)
    .Select(c => new {c.Title, ReleaseYear = c.Release_Year})
    .PartitionBy(c => c.Title)
    .ExecuteStatementAsync();

  var statementResponse = httpResponseMessage.ToStatementResponses();
}
```

Generated KSQL statement:
```KSQL
CREATE OR REPLACE TABLE MoviesByTitle
WITH ( KAFKA_TOPIC='moviesByTitle', KEY_FORMAT='Json', VALUE_FORMAT='Json', PARTITIONS='1', REPLICAS='1' )
AS SELECT Title, Release_Year AS ReleaseYear FROM Movies
WHERE Id < 3 PARTITION BY Title EMIT CHANGES;
```

### PartitionBy extension method (v0.9.0)
[Repartition a stream.](https://docs.ksqldb.io/en/0.15.0-ksqldb/developer-guide/joins/partition-data/)

### ExecuteStatementAsync extension method (v0.9.0)
Executes arbitrary [statements](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/#streams-and-tables):
```C#
async Task<HttpResponseMessage> ExecuteAsync(string statement)
{
  KSqlDbStatement ksqlDbStatement = new(statement);

  var httpResponseMessage = await restApiClient.ExecuteStatementAsync(ksqlDbStatement)
    .ConfigureAwait(false);

  string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();

  return httpResponseMessage;
}
```

### ToStatementString extension method (v0.9.0)
Generates ksql statement from Create(OrReplace)[Table|Stream]Statements
```C#
await using var context = new KSqlDBContext(@"http:\\localhost:8088");

var statement = context.CreateOrReplaceTableStatement(tableName: "MoviesByTitle")
  .As<Movie>()
  .Where(c => c.Id < 3)
  .Select(c => new {c.Title, ReleaseYear = c.Release_Year})
  .PartitionBy(c => c.Title)
  .ToStatementString();
```

Generated KSQL:
```KSQL
CREATE OR REPLACE TABLE MoviesByTitle
AS SELECT Title, Release_Year AS ReleaseYear FROM Movies
WHERE Id < 3 PARTITION BY Title EMIT CHANGES;
```

# v0.10.0:

### Window Bounds (v0.10.0)
The WHERE clause must contain a value for each primary-key column to retrieve and may optionally include bounds on WINDOWSTART and WINDOWEND if the materialized table is windowed.
```C#
using ksqlDB.RestApi.Client.KSql.Query.Functions;

string windowStart = "2019-10-03T21:31:16";
string windowEnd = "2025-10-03T21:31:16";

var result = await context.CreatePullQuery<IoTSensorStats>(MaterializedViewName)
  .Where(c => c.SensorId == "sensor-1")
  .Where(c => Bounds.WindowStart > windowStart && Bounds.WindowEnd <= windowEnd)
  .GetAsync();
```

Generated KSQL:
```KSQL
SELECT * FROM avg_sensor_values
WHERE SensorId = 'sensor-1' AND (WINDOWSTART > '2019-10-03T21:31:16') AND (WINDOWEND <= '2020-10-03T21:31:16');
```

### Pull queries - `ExecutePullQuery` (v.0.10.0)

Execute [pull query](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/select-pull-query/) with plain string query:
```C#
string ksql = "SELECT * FROM avg_sensor_values WHERE SensorId = 'sensor-1';";
var result = await context.ExecutePullQuery<IoTSensorStats>(ksql);
```

# v0.11.0:

### Creating streams and tables (v.0.11.0)
- [CREATE STREAM](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-stream/) - fluent API

```C#
EntityCreationMetadata metadata = new()
{
  KafkaTopic = nameof(MyMovies),
  Partitions = 1,
  Replicas = 1
};

string url = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(url));
var restApiClient = new KSqlDbRestApiClient(httpClientFactory);

var httpResponseMessage = await restApiClient.CreateStreamAsync<MyMovies>(metadata, ifNotExists: true);
```

```C#
public record MyMovies
{
  [ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations.Key]
  public int Id { get; set; }

  public string Title { get; set; }

  public int Release_Year { get; set; }
}
```

```KSQL
CREATE STREAM IF NOT EXISTS MyMovies (
	Id INT KEY,
	Title VARCHAR,
	Release_Year INT
) WITH ( KAFKA_TOPIC='MyMovies', VALUE_FORMAT='Json', PARTITIONS='1', REPLICAS='1' );
```

Create or replace alternative:

```C#
var httpResponseMessage = await restApiClient.CreateOrReplaceStreamAsync<MyMovies>(metadata);
```
 
- [CREATE TABLE](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-table/) - fluent API

```C#
EntityCreationMetadata metadata = new()
{
  KafkaTopic = nameof(MyMovies),
  Partitions = 2,
  Replicas = 3
};

string url = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(url));
var restApiClient = new KSqlDbRestApiClient(httpClientFactory);

var httpResponseMessage = await restApiClient.CreateTableAsync<MyMovies>(metadata, ifNotExists: true);
```

```KSQL
CREATE TABLE IF NOT EXISTS MyMovies (
	Id INT PRIMARY KEY,
	Title VARCHAR,
	Release_Year INT
) WITH ( KAFKA_TOPIC='MyMovies', VALUE_FORMAT='Json', PARTITIONS='2', REPLICAS='3' );
```

### Decimal precision
```C#
class Transaction
{
  [ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations.Decimal(3, 2)]
  public decimal Amount { get; set; }
}
```
Generated KSQL:
```KSQL
Amount DECIMAL(3,2)
```

# v1.0.0:

### Insert Into (v1.0.0)
[Insert values](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/insert-values/) - Produce a row into an existing stream or table and its underlying topic based on explicitly specified values.
```C#
string url = @"http:\\localhost:8088";

var http = new HttpClientFactory(new Uri(url));
var restApiClient = new KSqlDbRestApiClient(http);

var movie = new Movie() { Id = 1, Release_Year = 1988, Title = "Title" };

var response = await restApiClient.InsertIntoAsync(movie);
```

Properties and fields decorated with the IgnoreByInsertsAttribute are not part of the insert statements:
```C#
public class Movie
{
  [ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations.Key]
  public int Id { get; set; }
  public string Title { get; set; }
  public int Release_Year { get; set; }
	
  [ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations.IgnoreByInserts]
  public int IgnoredProperty { get; set; }
}
```

Generated KSQL:
```KSQL
INSERT INTO Movies (Title, Id, Release_Year) VALUES ('Title', 1, 1988);
```

### Insert values - FormatDoubleValue and FormatDecimalValue (v1.0.0)
```C#
var insertProperties = new InsertProperties()
{
  FormatDoubleValue = value => value.ToString("E1", CultureInfo.InvariantCulture),
  FormatDecimalValue = value => value.ToString(CultureInfo.CreateSpecificCulture("en-GB"))
};

public static readonly Tweet Tweet1 = new()
{
  Id = 1,
  Amount = 0.00042, 
  AccountBalance = 533333333421.6332M
};

await restApiProvider.InsertIntoAsync(tweet, insertProperties);
```

Generated KSQL statement:
```KSQL
INSERT INTO tweetsTest (Id, Amount, AccountBalance) VALUES (1, 4.2E-004, 533333333421.6332);
```

# **Breaking changes.**
In order to improve the v1.0.0 release the following methods and properties were renamed:

IKSqlDbRestApiClient interface changes:

| v0.11.0                 | v1.0.0                       |
|-------------------------|------------------------------|
| `CreateTable`           | `CreateTableAsync`           |
| `CreateStream`          | `CreateStreamAsync`          |
| `CreateOrReplaceTable`  | `CreateOrReplaceTableAsync`  |
| `CreateOrReplaceStream` | `CreateOrReplaceStreamAsync` |

KSQL documentation refers to stream or table name in FROM as [from_item](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/select-push-query/)

```
IKSqlDBContext.CreateQuery<TEntity>(string streamName = null)
IKSqlDBContext.CreateQueryStream<TEntity>(string streamName = null)
```
streamName parameters were renamed to fromItemName:
```
IKSqlDBContext.CreateQuery<TEntity>(string fromItemName = null)
IKSqlDBContext.CreateQueryStream<TEntity>(string fromItemName = null)
```
```
QueryContext.StreamName property was renamed to QueryContext.FromItemName
Source.Of parameter streamName was renamed to fromItemName
KSqlDBContextOptions.ShouldPluralizeStreamName was renamed to ShouldPluralizeFromItemName
```

Record.RowTime was decorated with IgnoreByInsertsAttribute

> ⚠  From version 1.0.0 the overridden from item names are pluralized, too. 
Join items are also affected by this breaking change. This breaking change can cause runtime exceptions for users updating from lower versions. In case that you have never used custom singular from-item names, your code won't be affected, see the example below:

```
var contextOptions = new KSqlDBContextOptions(@"http:\\localhost:8088")
{
  //Default value:  
  //ShouldPluralizeFromItemName = true
};

var query = new KSqlDBContext(contextOptions)
  .CreateQueryStream<Tweet>("Tweet")
  .ToQueryString();
```

KSQL generated since v1.0

```KSQL
SELECT * FROM Tweets EMIT CHANGES;
```

KSQL generated before v1.0

```KSQL
SELECT * FROM Tweet EMIT CHANGES;
```

# v1.1.0:

### CAST - ToString (v1.1.0)
Converts any type to its string representation.

```C#
var query = context.CreateQueryStream<Movie>()
  .GroupBy(c => c.Title)
  .Select(c => new { Title = c.Key, Concatenated = K.Functions.Concat(c.Count().ToString(), "_Hello") });
```

```KSQL
SELECT Title, CONCAT(CAST(COUNT(*) AS VARCHAR), '_Hello') Concatenated FROM Movies GROUP BY Title EMIT CHANGES;
```

### CAST - convert string to numeric types (v1.1.0)
```C#
using System;
using ksqlDB.RestApi.Client.KSql.Query.Functions;

Expression<Func<Tweet, int>> stringToInt = c => KSQLConvert.ToInt32(c.Message);
Expression<Func<Tweet, long>> stringToLong = c => KSQLConvert.ToInt64(c.Message);
Expression<Func<Tweet, decimal>> stringToDecimal = c => KSQLConvert.ToDecimal(c.Message, 10, 2);
Expression<Func<Tweet, double>> stringToDouble = c => KSQLConvert.ToDouble(c.Message);
```

```KSQL
CAST(Message AS INT)
CAST(Message AS BIGINT)
CAST(Message AS DECIMAL(10, 2))
CAST(Message AS DOUBLE)
```

### WithOffsetResetPolicy - push queries extension method (v1.1.0)
Overrides the AutoOffsetReset policy for the current query:
```C#
var subscription = context.CreateQueryStream<Movie>()
  .WithOffsetResetPolicy(AutoOffsetReset.Latest)
  .Subscribe(movie =>
  {
    Console.WriteLine($"{nameof(Movie)}: {movie.Id} - {movie.Title} - {movie.RowTime}");
  }, e => { Console.WriteLine($"Exception: {e.Message}"); });   
```

# v1.2.0:

### Connectors (v1.2.0)
GetConnectorsAsync - List all connectors in the Connect cluster.

DropConnectorAsync - Drop a connector and delete it from the Connect cluster. The topics associated with this cluster are not deleted by this command. The statement fails if the connector doesn't exist.
    
DropConnectorIfExistsAsync - Drop a connector and delete it from the Connect cluster. The topics associated with this cluster are not deleted by this command. The statement doesn't fail if the connector doesn't exist.

```C#
using System;
using System.Linq;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.RestApi.Client.KSql.RestApi.Extensions;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;

public async Task CreateGetAndDropConnectorAsync()
{
  var ksqlDbUrl = @"http:\\localhost:8088";

  var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

  var restApiClient = new KSqlDbRestApiClient(httpClientFactory);

  const string SinkConnectorName = "mock-connector";

  var createConnector = @$"CREATE SOURCE CONNECTOR `{SinkConnectorName}` WITH(
      'connector.class'='org.apache.kafka.connect.tools.MockSourceConnector');";

  var statement = new KSqlDbStatement(createConnector);

  var httpResponseMessage = await restApiClient.ExecuteStatementAsync(statement);

  var connectorsResponse = await restApiClient.GetConnectorsAsync();

  Console.WriteLine("Available connectors: ");
  Console.WriteLine(string.Join(',', connectorsResponse[0].Connectors.Select(c => c.Name)));

  httpResponseMessage = await restApiClient.DropConnectorAsync($"`{SinkConnectorName}`");

  // Or
  httpResponseMessage = await restApiClient.DropConnectorIfExistsAsync($"`{SinkConnectorName}`");
}
```

### Get streams (v1.2.0)
- IKSqlDbRestApiClient.GetStreamsAsync - List the defined streams.

```C#
var streamResponses = await restApiClient.GetStreamsAsync();

Console.WriteLine(string.Join(',', streamResponses[0].Streams.Select(c => c.Name)));
```

### Get tables (v1.2.0)
- IKSqlDbRestApiClient.GetTablesAsync - List the defined tables.

```C#
var tableResponses = await restApiClient.GetTablesAsync();

Console.WriteLine(string.Join(',', tableResponses[0].Tables.Select(c => c.Name)));
```

# v1.3.0:

### KSqlDbRestApiClient:

### Get topics (v1.3.0)
- GetTopicsAsync - lists the available topics in the Kafka cluster that ksqlDB is configured to connect to.
- GetAllTopicsAsync - lists all topics, including hidden topics.
- GetTopicsExtendedAsync - list of topics. Also displays consumer groups and their active consumer counts.
- GetAllTopicsExtendedAsync - list of all topics. Also displays consumer groups and their active consumer counts.

```C#
using System;
using System.Linq;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi.Responses.Topics;
using ksqlDB.RestApi.Client.Sample.Providers;

private static async Task GetKsqlDbInformationAsync(IKSqlDbRestApiProvider restApiProvider)
{
  Console.WriteLine($"{Environment.NewLine}Available topics:");
  var topicsResponses = await restApiProvider.GetTopicsAsync();
  Console.WriteLine(string.Join(',', topicsResponses[0].Topics.Select(c => c.Name)));

  TopicsResponse[] allTopicsResponses = await restApiProvider.GetAllTopicsAsync();
  TopicsExtendedResponse[] topicsExtendedResponses = await restApiProvider.GetTopicsExtendedAsync();
  var allTopicsExtendedResponses = await restApiProvider.GetAllTopicsExtendedAsync();
}
```

### Getting queries and termination of persistent queries (v1.3.0)
- GetQueriesAsync - Lists queries running in the cluster.

- TerminatePersistentQueryAsync - Terminate a persistent query. Persistent queries run continuously until they are explicitly terminated.

```C#
using System.Linq;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi;

private static async Task TerminatePersistentQueryAsync(IKSqlDbRestApiClient client)
{
  string topicName = "moviesByTitle";

  var queries = await client.GetQueriesAsync();

  var query = queries.SelectMany(c => c.Queries).FirstOrDefault(c => c.SinkKafkaTopics.Contains(topicName));

  var response = await client.TerminatePersistentQueryAsync(query.Id);
}
```

### Creating connectors (v1.3.0)
- CreateSourceConnectorAsync - Create a new source connector in the Kafka Connect cluster with the configuration passed in the config parameter.

- CreateSinkConnectorAsync - Create a new sink connector in the Kafka Connect cluster with the configuration passed in the config parameter.

See also how to create a SQL Server source connector with [SqlServer.Connector](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/SqlServer.Connector/Wiki.md)

```C#
using System.Collections.Generic;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.RestApi;

private static string SourceConnectorName => "mock-source-connector";
private static string SinkConnectorName => "mock-sink-connector";

private static async Task CreateConnectorsAsync(IKSqlDbRestApiClient restApiClient)
{
  var sourceConnectorConfig = new Dictionary<string, string>
  {
    {"connector.class", "org.apache.kafka.connect.tools.MockSourceConnector"}
  };

  var httpResponseMessage = await restApiClient.CreateSourceConnectorAsync(sourceConnectorConfig, SourceConnectorName);
      
  var sinkConnectorConfig = new Dictionary<string, string> {
    { "connector.class", "org.apache.kafka.connect.tools.MockSinkConnector" },
    { "topics.regex", "mock-sink*"},
  }; 		

  httpResponseMessage = await restApiClient.CreateSinkConnectorAsync(sinkConnectorConfig, SinkConnectorName);

  httpResponseMessage = await restApiClient.DropConnectorAsync($"`{SinkConnectorName}`");
}
```

# v1.4.0:

KSqlDbRestApiClient:

### Terminate push queries (v1.4.0)
- TerminatePushQueryAsync - terminates push query by query id

```C#
string queryId = "xyz123"; // <----- the ID of the query to terminate

var response = await restApiClient.TerminatePushQueryAsync(queryId);
```

### Drop a table (v1.4.0)
Drops an existing table.

```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));
var ksqlDbRestApiClient = new KSqlDbRestApiClient(httpClientFactory);

string tableName = "TableName";

// DROP TABLE TableName;
var httpResponseMessage = ksqlDbRestApiClient.DropTableAsync(tableName);

// OR DROP TABLE IF EXISTS TableName DELETE TOPIC;
httpResponseMessage = ksqlDbRestApiClient.DropTableAsync(tableName, useIfExistsClause: true, deleteTopic: true);
```

Parameters:

`useIfExistsClause` - If the IF EXISTS clause is present, the statement doesn't fail if the table doesn't exist.

`deleteTopic` - If the DELETE TOPIC clause is present, the table's source topic is marked for deletion.

### Drop a stream (v1.4.0)
Drops an existing stream.

```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));
var ksqlDbRestApiClient = new KSqlDbRestApiClient(httpClientFactory);

string streamName = "StreamName";

// DROP STREAM StreamName;
var httpResponseMessage = ksqlDbRestApiClient.DropStreamAsync(streamName);

// OR DROP STREAM IF EXISTS StreamName DELETE TOPIC;
httpResponseMessage = ksqlDbRestApiClient.DropStreamAsync(streamName, useIfExistsClause: true, deleteTopic: true);
```

Parameters:

`useIfExistsClause` - If the IF EXISTS clause is present, the statement doesn't fail if the stream doesn't exist.

`deleteTopic` - If the DELETE TOPIC clause is present, the stream's source topic is marked for deletion.

# v1.5.0:

### QbservableExtensions
## SubscribeAsync (v1.5.0)
- Subscribes an element handler, an exception handler, and a completion handler to an qbservable stream and asynchronously returns the query id.

## SubscribeOn (v1.5.0)
- Wraps the source sequence in order to run its subscription on the specified scheduler.

## ObserveOn (v1.5.0)
- Wraps the source sequence in order to run its observer callbacks on the specified scheduler.

```C#
using System;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.Sample.Models.Movies;

private static async Task SubscribeAsync(IKSqlDBContext context)
{
  var cts = new CancellationTokenSource();

  try
  {
    var subscription = await context.CreateQueryStream<Movie>()
      .SubscribeOn(ThreadPoolScheduler.Instance)
      .ObserveOn(TaskPoolScheduler.Default)
      .SubscribeAsync(onNext: movie =>
        {
          Console.WriteLine($"{nameof(Movie)}: {movie.Id} - {movie.Title} - {movie.RowTime}");
          Console.WriteLine();
        }, onError: error => { Console.WriteLine($"SubscribeAsync Exception: {error.Message}"); },
        onCompleted: () => Console.WriteLine("SubscribeAsync Completed"), cts.Token);

    Console.WriteLine($"Query id: {subscription}");
  }
  catch (Exception e)
  {
    Console.WriteLine(e);
  }
}
```

# v1.6.0:

## CreateTypeAsync (v1.6.0)
- `IKSqlDbRestApiClient.CreateTypeAsync<TEntity>` - Create an alias for a complex type declaration.

```C#
using System;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;
using ksqlDB.RestApi.Client.Sample.Models.Events;

private static async Task SubscriptionToAComplexTypeAsync()
{      
  var ksqlDbUrl = @"http:\\localhost:8088";

  var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));
  var restApiClient = new KSqlDbRestApiClient(httpClientFactory);

  var httpResponseMessage = await restApiClient.ExecuteStatementAsync(new KSqlDbStatement(@$"
Drop type {nameof(EventCategory)};
Drop table {nameof(Event)};
"));

  httpResponseMessage = await restApiClient.CreateTypeAsync<EventCategory>();
  httpResponseMessage = await restApiClient.CreateTableAsync<Event>(new EntityCreationMetadata { KafkaTopic = "Events", Partitions = 1 });
      
  await using var ksqlDbContext = new KSqlDBContext(new KSqlDBContextOptions(ksqlDbUrl));

  var subscription = ksqlDbContext.CreateQueryStream<Event>()
    .Take(1)
    .Subscribe(value =>
    {
      Console.WriteLine("Categories: ");

      foreach (var category in value.Categories)
      {
        Console.WriteLine($"{category.Name}");
      }
    }, error =>
    {
      Console.WriteLine(error.Message);
    });

  httpResponseMessage = await restApiClient.ExecuteStatementAsync(new KSqlDbStatement(@"
INSERT INTO Events (Id, Places, Categories) VALUES (1, ARRAY['1','2','3'], ARRAY[STRUCT(Name := 'Planet Earth'), STRUCT(Name := 'Discovery')]);"));
}
```

```C#
using System.Collections.Generic;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations;

record EventCategory
{
  public string Name { get; set; }
}

record Event
{
  [Key]
  public int Id { get; set; }

  public string[] Places { get; set; }

  public IEnumerable<EventCategory> Categories { get; set; }
}
```

## InsertIntoAsync for complex types (v1.6.0)
In v1.0.0 support for inserting entities with primitive types and strings was added. This version adds support for `List<T>` and records, classes and structs. 
Deeply nested types and dictionaries are not yet supported.

```C#
var testEvent = new EventWithList
{
  Id = "1",
  Places = new List<int> { 1, 2, 3 }
};

var ksqlDbUrl = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

var responseMessage = await new KSqlDbRestApiClient(httpClientFactory)
  .InsertIntoAsync(testEvent);
```
Generated KSQL:
```SQL
INSERT INTO EventWithLists (Id, Places) VALUES ('1', ARRAY[1,2,3]);
```

```C#
var eventCategory = new EventCategory
{
  Count = 1,
  Name = "Planet Earth"
};

var testEvent2 = new ComplexEvent
{
  Id = 1,
  Category = eventCategory
};

var responseMessage = await new KSqlDbRestApiClient(httpClientFactory)
  .InsertIntoAsync(testEvent2, new InsertProperties { EntityName = "Events" });
```

Generated KSQL:
```SQL
INSERT INTO Events (Id, Category) VALUES (1, STRUCT(Count := 1, Name := 'Planet Earth'));
```

## Operator IN - `IEnumerable<T>` and `IList<T>` Contains (v1.6.0)
Specifies multiple OR conditions.
`IList<T>`.Contains:
```C#
var orderTypes = new List<int> { 1, 2, 3 };

Expression<Func<OrderData, bool>> expression = o => orderTypes.Contains(o.OrderType);

```
Enumerable extension:
```C#
IEnumerable<int> orderTypes = Enumerable.Range(1, 3);

Expression<Func<OrderData, bool>> expression = o => orderTypes.Contains(o.OrderType);

```
For both options the following SQL is generated:
```SQL
OrderType IN (1, 2, 3)
```

# v1.7.0:

## IPullable - GetManyAsync (v1.7.0)
- `IPullable.GetManyAsync<TEntity>` - Pulls all values from the materialized view asynchronously and terminates. 

```C#
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq.PullQueries;
using ksqlDB.RestApi.Client.KSql.Query;
using ksqlDB.RestApi.Client.KSql.Query.Context;

public static async Task<List<OrderData>> GetOrdersAsync()
{
  var ksqlDbUrl = @"http:\\localhost:8088";
  var options = new KSqlDBContextOptions(ksqlDbUrl) { ShouldPluralizeFromItemName = false };
  options.QueryParameters.Properties["ksql.query.pull.table.scan.enabled"] = "true";

  await using var context = new KSqlDBContext(options);
  var tableName = "queryable_order";
  var orderTypes = new List<int> { 1,3 };

  var enumerable = context.CreatePullQuery<OrderData>(tableName)    
    .Where(o => o.EventTime >= 1630886400 && o.EventTime <= 1630887401 && orderTypes.Contains(o.OrderType))
    .GetManyAsync();

  List<OrderData> list = new List<OrderData>();

  await foreach (var item in enumerable.ConfigureAwait(false))
  {
    Console.WriteLine(item.ToString());
    list.Add(item);
  } 

  return list;
}
```
```C#
public class OrderData: Record
{
  public int Id { get; set; }
  public long EventTime  { get; set; }
  public int OrderType { get; set; }
  public string Description { get; set; }
}
```

## QbservableExtensions - ExplainAsync (v1.7.0)
- `ExplainAsync` - Show the execution plan for a SQL expression, show the execution plan plus additional runtime information and metrics.

```C#
using System;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.RestApi.Responses.Query.Descriptors;
using ksqlDB.RestApi.Client.Sample.Models.Movies;

public static async Task ExplainAsync(IKSqlDBContext context)
{
  var query = context.CreateQueryStream<Movie>()
    .Where(c => c.Title != "E.T.");

  string explain = await query
    .ExplainAsStringAsync();

  ExplainResponse[] explainResponses = await context.CreateQueryStream<Movie>().ExplainAsync();
      
  Console.WriteLine(explainResponses[0].QueryDescription.ExecutionPlan);
}
```

# v1.8.0:

### KSqlDbRestApiClient Droping types (v1.8.0)
- DropTypeAsync and DropTypeIfExistsAsync - Removes a type alias from ksqlDB. If the IF EXISTS clause is present, the statement doesn't fail if the type doesn't exist.

```C#
string typeName = nameof(EventCategory);
var httpResponseMessage = await restApiClient.DropTypeAsync(typeName);
//OR
httpResponseMessage = await restApiClient.DropTypeIfExistsAsync(typeName);
```

# KSqlDbRestApiClient ToInsertStatement (v1.8.0)
- Generates raw string Insert Into, but does not execute it.

```C#
Movie movie = new()
{
  Id = 1,
  Release_Year = 1986,
  Title = "Aliens"
};

var insertStatement = restApiProvider.ToInsertStatement(movie);

Console.WriteLine(insertStatement.Sql);
```

Output:

```SQL
INSERT INTO Movies (Title, Id, Release_Year) VALUES ('Aliens', 1, 1986);
```

### Operator (NOT) BETWEEN (v1.8.0)
KSqlOperatorExtensions - Between - Constrain a value to a specified range in a WHERE clause.

```C#
using ksqlDB.RestApi.Client.KSql.Query.Operators;

IQbservable<Tweet> query = context.CreateQueryStream<Tweet>()
  .Where(c => c.Id.Between(1, 5));
```

Generated KSQL:

```SQL
SELECT * FROM Tweets
WHERE Id BETWEEN 1 AND 5 EMIT CHANGES;
```

# v1.9.0:

## Lambda functions (Invocation functions) (v1.9.0)
- requirements: ksqldb 0.17.0
- This version covers ARRAY type. MAP types are not included in this release.

Lambda functions allow you to compose new expressions from existing ones. Lambda functions must be used inside the following invocation functions:
- **Transform**
- **Reduce**
- **Filter**

See also [Use lambda functions](https://docs.ksqldb.io/en/latest/how-to-guides/use-lambda-functions/) and [Invocation functions](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/scalar-functions/#invocation-functions)

The following example shows you how to take advantage of invocation functions with ksqlDB.RestApi.Client:

Add namespaces:
```C#
using System;
using System.Threading.Tasks;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Functions;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using ksqlDB.RestApi.Client.KSql.RestApi;
using ksqlDB.RestApi.Client.KSql.RestApi.Statements;
using ksqlDB.RestApi.Client.Sample.Models.InvocationFunctions;
```
Prepare the model:
```C#
record Lambda
{
  public int Id { get; set; }
  public int[] Lambda_Arr { get; set; }
}
```
Create the stream and insert a value:
```C#
public async Task PrepareAsync(IKSqlDbRestApiClient restApiClient)
{
  var statement =
    new KSqlDbStatement(
      @"CREATE STREAM stream2 (id INT, lambda_arr ARRAY<INTEGER>) WITH (kafka_topic = 'stream2', partitions = 1, value_format = 'json');");

  var createStreamResponse = await restApiClient.ExecuteStatementAsync(statement);

  var insertResponse = await restApiClient.ExecuteStatementAsync(
    new KSqlDbStatement("insert into stream2 (id, lambda_arr) values (1, ARRAY [1,2,3]);"));
}
```

Subscribe to the unbounded stream of events:
```C#
public IDisposable Invoke(IKSqlDBContext ksqlDbContext)
{
  var subscription = ksqlDbContext.CreateQuery<Lambda>(fromItemName: "stream2")
    .WithOffsetResetPolicy(AutoOffsetReset.Earliest)
    .Select(c => new
    {
      Transformed = KSqlFunctions.Instance.Transform(c.Lambda_Arr, x => x + 1),
      Filtered = KSqlFunctions.Instance.Filter(c.Lambda_Arr, x => x > 1),
      Acc = K.Functions.Reduce(c.Lambda_Arr, 0, (x, y) => x + y)
    }).Subscribe(c =>
    {
      Console.WriteLine($"Transformed array: {c.Transformed}");
      Console.WriteLine($"Filtered array: {c.Filtered}");
      Console.WriteLine($"Reduced array: {c.Acc}");
    }, error => { Console.WriteLine(error.Message); });

  return subscription;
}
```

The above query is equivalent to:
```KSQL
set 'auto.offset.reset' = 'earliest';

SELECT TRANSFORM(Lambda_Arr, (x) => x + 1) Transformed, FILTER(Lambda_Arr, (x) => x > 1) Filtered, REDUCE(Lambda_Arr, 0, (x, y) => x + y) Acc 
FROM stream2 
EMIT CHANGES;
```

Output:
```
+--------------------------------------+--------------------------------------+--------------------------------------+
|TRANSFORMED                           |FILTERED                              |ACC                                   |
+--------------------------------------+--------------------------------------+--------------------------------------+
|[2, 3, 4]                             |[2, 3]                                |6                                     |
```
 
### Transform arrays (v1.9.0)
- Transform a collection by using a lambda function.
- If the collection is an array, the lambda function must have one input argument.

```C#
record Tweets
{
  public string[] Messages { get; set; }
  public int[] Values { get; set; }
}
```

```C#
Expression<Func<Tweets, string[]>> expression = c => K.Functions.Transform(c.Messages, x => x.ToUpper());
```

```SQL
TRANSFORM(Messages, (x) => UCASE(x))
```

### Reduce arrays (v1.9.0) 
- Reduce a collection starting from an initial state.
- If the collection is an array, the lambda function must have two input arguments.
```C#
Expression<Func<Tweets, int>> expression = c => K.Functions.Reduce(c.Values, 0, (x,y) => x + y);
```

```SQL
REDUCE(Values, 0, (x, y) => x + y)
```

### Filter arrays (v1.9.0) 
- Filter a collection with a lambda function.
- If the collection is an array, the lambda function must have one input argument.
```C#
Expression<Func<Tweets, string[]>> expression = c => K.Functions.Filter(c.Messages, x => x == "E.T.");
```

```SQL
FILTER(Messages, (x) => x = 'E.T.')
```

## BYTES character type and ToBytes string function (v1.9.0)
- [The bytes type](https://docs.ksqldb.io/en/latest/reference/sql/data-types/#character-types) - represents an array of raw bytes.
- variable-length byte array in C# is represented as byte[]
- requirements: ksqldb 0.21.0

**ToBytes** - Converts a STRING value in the specified encoding to BYTES. The accepted encoders are 'hex', 'utf8', 'ascii' and 'base64'. Since: - ksqldb 0.21

```C#
Expression<Func<Tweet, byte[]>> expression = c => K.Functions.ToBytes(c.Message, "utf8");
```

Is equivalent to:
```KSQL
TO_BYTES(Message, 'utf8')
```

## FromBytes string function (v1.9.0)
- Converts a BYTES value to STRING in the specified encoding. The accepted encoders are 'hex', 'utf8', 'ascii' and 'base64'.

```C#
struct Thumbnail
{
  public byte[] Image { get; init; }
}
```
```C#
Expression<Func<Thumbnail, string>> expression = c => K.Functions.FromBytes(c.Image, "utf8");
```
Is equivalent to:
```KSQL
FROM_BYTES(Message, 'utf8')
```

## KSqlDbRestApiClient.InsertIntoAsync (v1.9.0)
- added support for ```IEnumerable<T>``` properties

```C#
record Order
{
  public int Id { get; set; }
  public IEnumerable<double> Items { get; set; }
}
```

```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));

var order = new Order { Id = 1, ItemsList = new List<double> { 1.1, 2 }};

var config = new InsertProperties
{
  ShouldPluralizeEntityName = false, 
  EntityName = "`my_order`"
};

var responseMessage = await new KSqlDbRestApiClient(httpClientFactory)
  .InsertIntoAsync(order, config);
```

Equivalent KSQL:
```SQL
INSERT INTO `my_order` (Id, ItemsList) VALUES (1, ARRAY[1.1,2]);
```

### Inserting empty arrays (v1.9.0)
- empty arrays are generated in the following way (workaround)

```C#
var order = new Order { Id = 1, ItemsList = new List<double>()};

var responseMessage = await new KSqlDbRestApiClient(httpClientFactory)
  .InsertIntoAsync(order);
```

```SQL
ARRAY_REMOVE(ARRAY[0], 0))
```

```ARRAY[]``` is not yet supported in ksqldb (v0.21.0)

# v1.10.0:

## Lambda functions (Invocation functions) - Maps (v1.10.0)

Model:
```C#
record Lambda
{
  public IDictionary<string, int[]> DictionaryArrayValues { get; set; }
  public IDictionary<string, int> DictionaryInValues { get; set; }
}
```

### Transform maps (v1.10.0)
Transform a collection by using a lambda function.
If the collection is a map, two lambda functions must be provided, and both lambdas must have two arguments: a map entry key and a map entry value.

```C#
Expression<Func<Lambda, IDictionary<string, int[]>>> expression = 
    c => K.Functions.Transform(c.Dictionary, (k, v) => K.Functions.Concat(k, "_new"), (k, v) => K.Functions.Transform(v, x => x * x));
```

Equivalent KSQL:
```SQL
TRANSFORM(DictionaryArrayValues, (k, v) => CONCAT(k, '_new'), (k, v) => TRANSFORM(v, (x) => x * x))
```

### Filter maps (v1.10.0)
Filter a collection with a lambda function.
If the collection is a map, the lambda function must have two input arguments.

```C#
Expression<Func<Lambda, IDictionary<string, int>>> expression = 
    c => K.Functions.Filter(c.Dictionary2, (k, v) => k != "E.T" && v > 0);
```

Equivalent KSQL:
```SQL
FILTER(DictionaryInValues, (k, v) => (k != 'E.T') AND (v > 0))
```

### Reduce maps (v1.10.0)
Reduce a collection starting from an initial state.
If the collection is a map, the lambda function must have three input arguments.
If the state is null, the result is null.

```C#
Expression<Func<Lambda, int>> expression = 
    c => K.Functions.Reduce(c.Dictionary2, 2, (s, k, v) => K.Functions.Ceil(s / v));
```

Equivalent KSQL:
```SQL
REDUCE(DictionaryInValues, 2, (s, k, v) => CEIL(s / v))
```

### IKSqlGrouping.Source (v1.10.0)
- grouping by nested properies. Can be used in the following way:

```C#
var source = Context.CreateQueryStream<City>()
  .WithOffsetResetPolicy(AutoOffsetReset.Earliest)
  .GroupBy(c => new { c.RegionCode, c.State.Name })
  .Select(g => new { g.Source.RegionCode, g.Source.State.Name, Count = g.Count()})
  .Take(1)
  .ToAsyncEnumerable();
```

```C#
record City
{
  [Key]
  public string RegionCode { get; init; }
  public State State { get; init; }
}

record State
{
  public string Name { get; init; }
}
```

Equivalent KSQL:
```SQL
SELECT RegionCode, State->Name, COUNT(*) Count 
FROM Cities 
GROUP BY RegionCode, State->Name 
EMIT CHANGES;
```

### Query syntax
Note that ksqldb does not support OrderBy
```C#
var grouping = 
  from city in context.CreateQueryStream<City>()
  where city.RegionCode != "xy"
  group city by city.State.Name into g
  select new
  {
    g.Source.RegionCode,
    g.Source.State.Name,
    Num_Times = g.Count()
  };
```

# v2.0.0:

## Breaking change KSqlDBContextOptions
> ⚠ KSqlDBContextOptions created with a constructor or by KSqlDbContextOptionsBuilder are setting the auto.offset.reset to earliest by default. This version removes this default configuration. It will not be opinionated in this way from now.
> This will affect your subscriptions to streams.

You can set it back in the following way:

```C#
var contextOptions = new KSqlDbContextOptionsBuilder()
  .UseKSqlDb(ksqlDbUrl)
  .SetAutoOffsetReset(AutoOffsetReset.Earliest)
  .Options;
```

### ProcessingGuarantee enum (v2.0.0)
**ExactlyOnce** - Records are processed once. To achieve a true exactly-once system, end consumers and producers must also implement exactly-once semantics.
**AtLeastOnce** - Records are never lost but may be redelivered.

For more info check [exactly once semantics](https://docs.ksqldb.io/en/latest/operate-and-deploy/exactly-once-semantics/)

```C#
public enum ProcessingGuarantee
{
  ExactlyOnce,
  AtLeastOnce
}
```

### KSqlDbContextOptionsBuilder SetProcessingGuarantee (v2.0.0)
Enable exactly-once or at_least_once semantics

```C#
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Context.Options;
using ksqlDB.RestApi.Client.KSql.Query.Options;
```
```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var contextOptions = new KSqlDbContextOptionsBuilder()
  .UseKSqlDb(ksqlDbUrl)
  .SetProcessingGuarantee(ProcessingGuarantee.AtLeastOnce)
  .Options;

await using var context = new KSqlDBContext(contextOptions);
```

## Basic auth (v2.0.0)
In ksqldb you can use the [Http-Basic authentication](https://docs.ksqldb.io/en/latest/operate-and-deploy/installation/server-config/security/#configuring-listener-for-http-basic-authenticationauthorization) mechanism:
```C#
string ksqlDbUrl = @"http:\\localhost:8088";

string userName = "fred";
string password = "letmein";

var options = ClassUnderTest.UseKSqlDb(ksqlDbUrl)
  .SetBasicAuthCredentials(userName, password)
  .Options;

await using var context = new KSqlDBContext(options);
```

See also how to [intercept http requests](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/wiki/Interception-of-HTTP-requests-in-ksqlDB.RestApi.Client---Authentication)

```C#
var httpClientFactory = new HttpClientFactory(new Uri(ksqlDbUrl));
      
var restApiClient = new KSqlDbRestApiClient(httpClientFactory)
  .SetCredentials(new BasicAuthCredentials("fred", "letmein"));
```

## `IPullable<T>.FirstOrDefaultAsync` (v2.0.0)
`IPullable<T>.GetAsync` was renamed to `IPullable<T>.FirstOrDefaultAsync`

```C#
using ksqlDB.RestApi.Client.KSql.Linq.PullQueries;

private static async Task GetAsync(IPullable<IoTSensorStats> pullQuery)
{
  var result = await pullQuery
    .FirstOrDefaultAsync();

  Console.WriteLine(
    $"Pull query GetAsync result => Id: {result?.SensorId} - Avg Value: {result?.AvgValue} - Window Start {result?.WindowStart}");
}
```

## KSqlDbRestApiClient.InsertIntoAsync
- added support for deeply nested types - Maps, Structs and Arrays

```C#
var value = new ArrayOfMaps
{
  Arr = new[]
        {
          new Dictionary<string, int> { { "a", 1 }, { "b", 2 } },
          new Dictionary<string, int> { { "c", 3 }, { "d", 4 } }
        }
};

httpResponseMessage = await restApiClient.InsertIntoAsync(value);
```

```C#
record ArrayOfMaps
{
  public Dictionary<string, int>[] Arr { get; set; }
}
```

## Qbservable.Select
- generation of values from captured variables

```C#
var value = new FooClass { Property = 42 };

var query = context.CreateQueryStream<Location>()
    .Select(_ => new
    {
      Value = value
    });
```

Is equivalent with:
```SQL
SELECT STRUCT(Property := 42) AS Value FROM Locations EMIT CHANGES;
```

# ksqldb.RestApi.Client v1.0.0
⚠ The package had to be renamed from Kafka.DotNet.ksqlDB to ksqlDB.RestApi.Client

# ksqldb.RestApi.Client v1.1.0

### multiple joins with query comprehension syntax (GroupJoin, SelectMany, DefaultIfEmpty)

```C#
using ksqlDB.RestApi.Client.KSql.RestApi.Statements.Annotations;

class Order
{
  public int OrderId { get; set; }
  public int PaymentId { get; set; }
  public int ShipmentId { get; set; }
}

class Payment
{
  [Key]
  public int Id { get; set; }
}

record Shipment
{
  [Key]
  public int? Id { get; set; }
}
```

```C#
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
```

```C#
var ksqlDbUrl = @"http:\\localhost:8088";

var context = new KSqlDBContext(ksqlDbUrl);

var query = (from o in context.CreateQueryStream<Order>()
    join p1 in Source.Of<Payment>() on o.PaymentId equals p1.Id
    join s1 in Source.Of<Shipment>() on o.ShipmentId equals s1.Id into gj
    from sa in gj.DefaultIfEmpty()
    select new
           {
             orderId = o.OrderId,
             shipmentId = sa.Id,
             paymentId = p1.Id,
           })
  .Take(5);
```

Equivalent KSQL:

```SQL
SELECT o.OrderId AS orderId, sa.Id AS shipmentId, p1.Id AS paymentId FROM Orders o
INNER JOIN Payments p1
ON O.PaymentId = p1.Id
LEFT JOIN Shipments sa
ON o.ShipmentId = sa.Id
EMIT CHANGES LIMIT 5;
```

Creation of entities for the above mentioned query:

```C#
var entityCreationMetadata = new EntityCreationMetadata
                             {
                               KafkaTopic = nameof(Order) + "-Join",
                               Partitions = 1
                             };

var response = await restApiClient.CreateStreamAsync<Order>(entityCreationMetadata, ifNotExists: true);
response = await restApiClient.CreateTableAsync<Payment>(entityCreationMetadata with { KafkaTopic = nameof(Payment) }, ifNotExists: true);
response = await restApiClient.CreateTableAsync<Shipment>(entityCreationMetadata with { KafkaTopic = nameof(Shipment) }, ifNotExists: true);
```

Listen to the incoming record messages:

```C#
using var subscription = query
  .Subscribe(c => {
               Console.WriteLine($"{nameof(Order.OrderId)}: {c.orderId}");

               Console.WriteLine($"{nameof(Order.PaymentId)}: {c.paymentId}");

               if (c.shipmentId.HasValue)
                 Console.WriteLine($"{nameof(Order.ShipmentId)}: {c.shipmentId}");

             }, error => {
                  Console.WriteLine(error.Message);
                });
```

Inserting of sample data:

```C#
var order = new Order { OrderId = 1, PaymentId = 1, ShipmentId = 1 };
var payment = new Payment { Id = 1 };
var shipment = new Shipment { Id = 1 };

response = await restApiClient.InsertIntoAsync(order);
response = await restApiClient.InsertIntoAsync(payment);
response = await restApiClient.InsertIntoAsync(shipment);
```

Left joins can be also constructed in the following (less readable) way:

```C#
var query2 = KSqlDBContext.CreateQueryStream<Order>()
  .GroupJoin(Source.Of<Payment>(), c => c.OrderId, c => c.Id, (order, gj) => new
                                                                             {
                                                                               order,
                                                                               grouping = gj
                                                                             })
  .SelectMany(c => c.grouping.DefaultIfEmpty(), (o, s1) => new
                                                           {
                                                             o.order.OrderId,
                                                             shipmentId = s1.Id,
                                                           });
```

Equivalent KSQL:

```KSQL
SELECT order.OrderId OrderId, s1.Id AS shipmentId FROM Orders order
LEFT JOIN Payments s1
ON order.OrderId = s1.Id
EMIT CHANGES;
```

# ksqldb.RestApi.Client v1.2.0

### Logging info and ConfigureKSqlDb

Bellow code demonstrates two new concepts. Logging and registration of services.

`KSqlDbServiceCollectionExtensions.ConfigureKSqlDb` - registers the following dependencies:

- IKSqlDBContext with Scoped ServiceLifetime. Can be altered with `contextLifetime` parameter.
- IKSqlDbRestApiClient with Scoped ServiceLifetime.
- IHttpClientFactory with Singleton ServiceLifetime.
- KSqlDBContextOptions with Singleton ServiceLifetime.

```XML
<PackageReference Include="Microsoft.Extensions.Hosting" Version="5.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="5.0.1" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="5.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="5.0.0" />
```

```C#
using System.Threading.Tasks;
using ksqlDB.Api.Client.Samples.HostedServices;
using ksqlDb.RestApi.Client.DependencyInjection;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ksqlDB.Api.Client.Samples
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
      Host.CreateDefaultBuilder(args)
        .ConfigureLogging((hostingContext, logging) =>
                          {
                            logging.AddConsole();
                            logging.AddDebug();
                          })
        .ConfigureServices((hostContext, serviceCollection) =>
                           {
                             var ksqlDbUrl = @"http:\\localhost:8088";

                             var setupAction = setupParameters =>
                                               {
                                                   setupParameters.SetAutoOffsetReset(AutoOffsetReset.Earliest);
                                               };

                             serviceCollection.ConfigureKSqlDb(ksqlDbUrl, setupAction);

                             serviceCollection.AddHostedService<Worker>();
                           });
  }
}
```

appsettings.json

```JSON
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "ksqlDb.RestApi.Client": "Information" // "Debug"
    }
  }
}
```

```C#
using System;
using System.Threading;
using System.Threading.Tasks;
using ksqlDB.Api.Client.Samples.Models.Movies;
using ksqlDB.RestApi.Client.KSql.Linq;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using ksqlDB.RestApi.Client.KSql.Query.Options;
using ksqlDB.RestApi.Client.KSql.RestApi;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : IHostedService, IDisposable
{
  private readonly IKSqlDBContext context;
  private readonly IKSqlDbRestApiClient restApiClient;
  private readonly ILogger logger;

  public Worker(IKSqlDBContext context, IKSqlDbRestApiClient restApiClient, ILoggerFactory loggerFactory)
  {
    this.context = context ?? throw new ArgumentNullException(nameof(context));
    this.restApiClient = restApiClient ?? throw new ArgumentNullException(nameof(restApiClient));

    logger = loggerFactory.CreateLogger<Worker>();
  }

  private Subscription subscription;

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    logger.LogInformation("Starting");

    subscription = await context.CreateQueryStream<Movie>()
      .WithOffsetResetPolicy(AutoOffsetReset.Earliest)
      .SubscribeAsync(
        onNext: movie => { },
        onError: e => { },
        onCompleted: () => { },
        cancellationToken: cancellationToken);
  }
  
  public Task StopAsync(CancellationToken cancellationToken)
  {
    logger.LogInformation("Stopping.");

    return Task.CompletedTask;
  }

  public void Dispose()
  {
  }
}
```

# ksqldb.RestApi.Client v1.3.0

### Join within

- specifies a time window for stream-stream joins

```C#
var query = from o in KSqlDBContext.CreateQueryStream<Order>()
  join p in Source.Of<Payment>().Within(Duration.OfHours(1), Duration.OfDays(5)) on o.OrderId equals p.Id
  select new
         {
           orderId = o.OrderId,
           paymentId = p.Id
         };
```

```SQL
SELECT o.OrderId AS orderId, p.Id AS paymentId FROM Orders o
INNER JOIN Payments p
WITHIN (1 HOURS, 5 DAYS) ON o.OrderId = p.Id
EMIT CHANGES;
```

### Operator LIKE - String.StartsWith, String.EndsWith, String.Contains

Match a string with a specified pattern:

```C#
var query = context.CreateQueryStream<Movie>()
  .Where(c => c.Title.ToLower().Contains("hard".ToLower());
```

```SQL
SELECT * FROM Movies
WHERE LCASE(Title) LIKE LCASE('%hard%') EMIT CHANGES;
```

```C#
var query = context.CreateQueryStream<Movie>()
  .Where(c => c.Title.StartsWith("Die");
```

```SQL
SELECT * FROM Movies
WHERE Title LIKE 'Die%' EMIT CHANGES;
```

## IKSqlDBContext Add and SaveChangesAsync

With IKSqlDBContext.Add and IKSqlDBContext.SaveChangesAsync you can add multiple entities to the context and save them asynchronously in one request (as "batch inserts").

```C#
private static async Task AddAndSaveChangesAsync(IKSqlDBContext context)
{
  context.Add(new Movie { Id = 1 });
  context.Add(new Movie { Id = 2 });

  var saveResponse = await context.SaveChangesAsync();
}
```   

# ksqldb.RestApi.Client v1.3.1

### InsertProperties IncludeReadOnlyProperties

- Inserts - include readonly properties configuration

The initial convention is that all writeable public instance properties and fields are taken into account during the Insert into statement generation.

```C#
public record Foo
{
  public Foo(string name)
  {
    Name = name;
  }

  public string Name { get; }
  public int Count { get; set; }
}
```

```C#
var insertProperties = new InsertProperties
                       {
                         IncludeReadOnlyProperties = true
                       };

await using KSqlDBContext context = new KSqlDBContext(@"http:\\localhost:8088");

var model = new Foo("Bar") {
  Count = 3
};

context.Add(model, insertProperties);

var responseMessage = await context.SaveChangesAsync();
```

# v1.4.0
## KSqlDbServiceCollectionExtensions - AddDbContext and AddDbContextFactory

- AddDbContext - Registers the given ksqldb context as a service in the IServiceCollection
- AddDbContextFactory - Registers the given ksqldb context factory as a service in the IServiceCollection

```C#
using ksqlDB.Api.Client.Samples;
using ksqlDB.Api.Client.Samples.Models.Movies;
using ksqlDb.RestApi.Client.DependencyInjection;
using ksqlDB.RestApi.Client.KSql.Query.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
```

```C#
var serviceCollection = new ServiceCollection();

var ksqlDbUrl = @"http:\\localhost:8088";

serviceCollection.AddDbContext<ApplicationKSqlDbContext, IApplicationKSqlDbContext>(options =>
  options.UseKSqlDb(ksqlDbUrl), contextLifetime: ServiceLifetime.Transient);

serviceCollection.AddDbContextFactory<IApplicationKSqlDbContext>(factoryLifetime: ServiceLifetime.Scoped);
```

```C#
internal class ApplicationKSqlDbContext : KSqlDBContext, Program.IApplicationKSqlDbContext
{
  public ApplicationKSqlDbContext(string ksqlDbUrl, ILoggerFactory loggerFactory = null)
    : base(ksqlDbUrl, loggerFactory)
  {
  }

  public ApplicationKSqlDbContext(KSqlDBContextOptions contextOptions, ILoggerFactory loggerFactory = null)
    : base(contextOptions, loggerFactory)
  {
  }

  public ksqlDB.RestApi.Client.KSql.Linq.IQbservable<Movie> Movies => CreateQueryStream<Movie>();
}

public interface IApplicationKSqlDbContext : IKSqlDBContext
{
  ksqlDB.RestApi.Client.KSql.Linq.IQbservable<Movie> Movies { get; }
}
```

### IKSqlDBContextFactory
A factory for creating derived KSqlDBContext instances.

```C#
var contextFactory = serviceCollection.BuildServiceProvider().GetRequiredService<IKSqlDBContextFactory<IKSqlDBContext>>();

var context = contextFactory.Create();
```

## IKSqlDbRestApiClient CreateSourceStreamAsync and CreateSourceTableAsync
- CreateSourceStreamAsync - creates a read-only stream
- CreateSourceTableAsync - creates a read-only table

```C#
string entityName = nameof(IoTSensor;

var metadata = new EntityCreationMetadata(entityName, 1)
               {
                 EntityName = entityName
               };

var httpResponseMessage = await restApiClient.CreateSourceTableAsync<IoTSensor>(metadata, ifNotExists: true);
```

## KSqlDbContextOptionsBuilder and KSqlDbContextOptions
- SetJsonSerializerOptions - a way to configure the JsonSerializerOptions for the materialization of the incoming values.

For better performance you can use the new `System.Text.Json` source generator:

```C#
var contextOptions = new KSqlDbContextOptionsBuilder()
        .UseKSqlDb(ksqlDbUrl)
        .SetJsonSerializerOptions(c =>
        {
          c.Converters.Add(new CustomJsonConverter());

          jsonOptions.AddContext<SourceGenerationContext>();
        }).Options;

//or
contextOptions = new KSqlDBContextOptions(ksqlDbUrl)
  .SetJsonSerializerOptions(serializerOptions =>
                            {
                              serializerOptions.Converters.Add(new CustomJsonConverter());

                              jsonOptions.AddContext<SourceGenerationContext>();
                            });
```

```C#
using System.Text.Json.Serialization;
using ksqlDB.Api.Client.Samples.Models.Movies;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Movie))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
```

# v1.5.0

- improved invocation function extensions

```C#
var ksql = ksqlDbContext.CreateQueryStream<Lambda>()
  .Select(c => new
  {
    Transformed = c.Lambda_Arr.Transform(x => x + 1),
    Filtered = c.Lambda_Arr.Filter(x => x > 1),
    Acc = c.Lambda_Arr.Reduce(0, (x, y) => x + y)
  })
  .ToQueryString();
```

```C#
record Lambda
{
  public int Id { get; set; }
  public int[] Lambda_Arr { get; set; }

  public IDictionary<string, int[]> DictionaryArrayValues { get; set; }
  public IDictionary<string, int> DictionaryInValues { get; set; }
}
```

## operator Between for Time type values
```C#
var from = new TimeSpan(11, 0, 0);
var to = new TimeSpan(15,0 , 0);

Expression<Func<MyTimeSpan, TimeSpan>> expression = t => t.Ts.Between(from, to);
```

```SQL
Ts BETWEEN '11:00:00' AND '15:00:00'
```

```C#
var from = new TimeSpan(11, 0, 0);
var to = new TimeSpan(15, 0, 0);

var query = context.CreateQueryStream<MyClass>()
  .Where(c => c.Ts.Between(from, to))
  .Select(c => new { c.Ts, to, FromTime = from, DateTime.Now, New = new TimeSpan(1, 0, 0) }
  .ToQueryString();
```

# v2.2.1

- Renaming of stream or table column names with the `JsonPropertyNameAttribute` was also added for selects

**Data definititions:**
- [Headers](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/data_definitions.md#access-record-header-data-v160)

**List of supported data types:**
- [Time types DATE, TIME AND TIMESTAMP]()
- [System.GUID as ksqldb VARCHAR type](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/data_types.md#systemguid-as-ksqldb-varchar-type-v240)

**List of supported Joins:**
- [RightJoin](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/joins.md#rightjoin)

List of supported [pull query](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/pull_queries.md) extension methods:
- [Take (LIMIT)](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/pull_queries.md#pull-query-take-extension-method-limit)

**List of supported ksqlDB SQL statements:**
- [Pause and resume persistent qeries](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#pause-and-resume-persistent-qeries-v250)
- [InsertProperties.UseInstanceType](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#insertpropertiesuseinstancetype)
- [Added support for extracting field names and values (for insert and select statements)](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#added-support-for-extracting-field-names-and-values-for-insert-and-select-statements)
- [AssertTopicExistsAsync](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#iksqldbrestapiclientasserttopicexistsasync-and-iksqldbrestapiclientasserttopicnotexistsasync)
- [AssertSchemaExistsAsync](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#iksqldbrestapiclientassertschemaexistsasync-and-iksqldbrestapiclientassertschemanotexistsasync)
- [Rename stream or table column names with the `JsonPropertyNameAttribute`](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/statements.md#rename-stream-or-table-column-names-with-the-jsonpropertynameattribute)

**Config:**
- [KSqlDbContextOptionsBuilder.ReplaceHttpClient](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/config.md#ksqldbcontextoptionsbuilderreplacehttpclient)

**Miscelenaous:**
- [Change data capture](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/cdc.md)
- [List of breaking changes](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/blob/main/doc/breaking_changes.md)
# LinqPad samples
[Push Query](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/ksqlDB.RestApi.Client.LinqPad/ksqlDB.RestApi.Client.linq)

[Pull Query](https://github.com/tomasfabian/ksqlDB.RestApi.Client-DotNet/tree/main/Samples/ksqlDB.RestApi.Client.LinqPad/ksqlDB.RestApi.Client.pull-query.linq)

# Nuget
https://www.nuget.org/packages/ksqlDB.RestApi.Client/

**TODO:**
- [CREATE TABLE AS SELECT](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/create-table-as-select/) - EMIT output_refinement
- rest of the [ksql query syntax](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/select-push-query/) (supported operators etc.)

# ksqldb links
[Scalar functions](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/scalar-functions/#as_value)

[Aggregation functions](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/aggregate-functions/)

[Push query](https://docs.ksqldb.io/en/latest/developer-guide/ksqldb-reference/select-push-query/)

# Acknowledgements:
- [ksql](https://github.com/confluentinc/ksql)

- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/)
- [Pluralize.NET](https://www.nuget.org/packages/Pluralize.NET/)
- [System.Interactive.Async](https://www.nuget.org/packages/System.Interactive.Async/)
- [System.Reactive](https://www.nuget.org/packages/System.Reactive/)
- [System.Text.Json](https://www.nuget.org/packages/System.Text.Json/)

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/tomasfabian)
