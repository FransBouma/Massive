Masssive, a small, happy, dynamic MicroORM for .NET that will love you forever
===============================================================================

Massive was started by Rob Conery and [has been transfered](https://twitter.com/robconery/status/573139252487323648) to Frans Bouma on March 4th, 2015. It's a small MicroORM based on the `Expando` or `dynamic` type and allows you to work with your database with almost no effort. The design is based on the idea that the code provided to you in this repository is a start: you get up and running in no-time and from there edit and alter it as you see fit.  

## Current Status
Massive is currently on v2.0. To obtain the old v1.0 code, please select the v1.0 branch in the GitHub menu or click [here](https://github.com/FransBouma/Massive/tree/v1.0).

## Installation
To use Massive in your project simply download the following files from the repository's [src](https://github.com/FransBouma/Massive/tree/v2.0/src) folder:

* Massive.Shared.cs
* Massive._YourDatabase_.cs, e.g. Massive.SqlServer.cs for SQL Server
* Massive.Shared.Async.cs, if you want to use the Massive API asynchronously. Requires .NET 4.5 or higher.

Due to its design, all files share the same namespace. If you need to use more than one database in your project, you have to change the namespace of the files and use per database a version of the Massive.Shared.* files. 

## Requirements
Massive has no external direct dependencies, just get the code, compile it with your project and enjoy. It does have an _indirect_ dependency, namely on the ADO.NET provider of the database used. The ADO.NET provider is obtained through .NET's `DbProviderFactory` system. This requires that the ADO.NET provider has been setup properly so .NET can find the factory needed. The following ADO.NET providers are supported out of the box. 

* SQL Server. The ADO.NET provider ships with .NET.
* Oracle: [ODP.NET](http://www.oracle.com/technetwork/topics/dotnet/index-085163.html). The installer for ODP.NET v12c installs both a full managed ADO.NET provider and a wrapper around its Java based Client Level Interface (CLI). By default Massive uses the latter, using the factory name `Oracle.DataAccess.Client`. If you want to use the Managed provider, please change the value in property `DbProviderFactoryName` in the [Massive.Oracle.cs](https://github.com/FransBouma/Massive/blob/v2.0/src/Massive.Oracle.cs) file around [line 297](https://github.com/FransBouma/Massive/blob/v2.0/src/Massive.Oracle.cs#L297) to `Oracle.ManagedDataAccess.Client`. This requires .NET 4.0 or higher.
* PostgreSQL: [Npgsql](http://www.npgsql.org/). By default the Npgsql distribution doesn't install any DbProviderFactory, so you have to do this manually. 
* SQLite: Massive uses the official SQLite .NET provider. [Please read the official documentation](https://system.data.sqlite.org/index.html/doc/trunk/www/index.wiki) on that provider to get started. 

## Migrating from v1.0 to v2.0
If you're using v1.0 currently and want to migrate to v2.0, please take a look at [What's new in v2.0](https://github.com/FransBouma/Massive/wiki/v2.0-Whats-new) page for more details about whether you'll run into the changes made. In general the breaking changes will be minor, if any. 

## What's new in v2.0
Besides some changes as documented in the [breaking changes](https://github.com/FransBouma/Massive/wiki/v2.0-Whats-new), the following features / additions are new:

* Async / Await support. Not all supported databases support asynchronous data-access under the hood, but the Massive API at least allows you to work with the code asynchronously. Full Async is supported by the ADO.NET providers of SQL Server and Npgsql (3.x). ODP.NET (Oracle) doesn't support async under the hood so using the Async API with Oracle will still use synchronous data-access under the hood (through the default DbCommand fall back code). SQLite's ADO.NET provider does support async using specific types but Massive doesn't support these.
* Shared code. In v1.0 code which was effectively the same among all supported databases was copy/pasted, in v2.0 Massive uses partial classes and shares as much code as possible among all supported databases. 
* Unit Tests. In v1.0 there were no tests but in v2.0 we properly implemented a series of tests to see whether things indeed work the way they do. They can also be used as an example how to get started. 
* Culling of dead code. 

## Usage
Note, the following is a work in progress and doesn't contain all the new API methods. It is primarily the original text written by Conery, and I'll update it when I have time. If you're unsure how to use a given method, please look at the [tests](https://github.com/FransBouma/Massive/tree/v2.0/tests).  

Massive is a "wrapper" for your DB tables and uses System.Dynamic extensively. If you try to use this with C# 3.5 or below, it will explode and you will be sad. Me too honestly - I like how this doesn't require any DLLs other than what's in the GAC. Yippee.

 * Get a Database. Northwind will work nicely. Add a connection to your database in your web.config (or app.config). Don't forget the providerName! If you don't know what that is - just add providerName = 'System.Data.SqlClient' right after the whole connectionString stuff.
 * Create a class that wraps a table. You can call it whatever you like, but if you want to be cool just name it the same as your table.
 * Query away and have fun

Code Please
-----------
Let's say we have a table named "Products". You create a class like this:

```csharp
public class Products:DynamicModel {
	//you don't have to specify the connection - Massive will use the first one it finds in your config
	public Products():base("northwind", "products","productid") {}
}
```

You could also just instantiate it inline, as needed:

```csharp
var tbl = new DynamicModel("northwind", tableName:"Products", primaryKeyField:"ProductID");
```

Or ignore the object hierarchy altogether:
	
```csharp
Massive.DB.Current.Query(...);
```

Now you can query thus:

```csharp
var table = new Products();
//grab all the products
var products = table.All();
//just grab from category 4. This uses named parameters
var productsFour = table.All(columns: "ProductName as Name", where: "WHERE categoryID=@0",args: 4);
```

That works, but Massive is "dynamic" - which means that it can figure a lot of things out on the fly. That query above can be rewritten like this:

```csharp
dynamic table = new Products(); //"dynamic" is important here - don't use "var"!
var productsFour = table.Find(CategoryID:4,columns:"ProductName");
```
	
The "Find" method doesn't exist, but since Massive is dynamic it will try to infer what you mean by using DynamicObject's TryInvokeMember. See the source for more details. There's more on the dynamic query stuff down below.
	
You can also run ad-hoc queries as needed:

```csharp
var result = tbl.Query("SELECT * FROM Categories");
```

This will pull categories and enumerate the results - streaming them as opposed to bulk-fetching them (thanks to Jeroen Haegebaert for the code). 

If you want to have a paged result set - you can:

```csharp
var result = tbl.Paged(where: "UnitPrice > 20", currentPage:2, pageSize: 20);
```

In this example, ALL of the arguments are optional and default to reasonable values. CurrentPage defaults to 1, pageSize defaults to 20, where defaults to nothing.

What you get back is a Dynamic with three properties: Items, TotalPages and TotalRecords. Items is a Query which is lazily evaluated and you can enumerate it after casting it to `IEnumerable<dynamic>`. TotalPages is the total number of pages in the complete result set and TotalRecords is the total number of records in the result set. What's in the Items collection is totally up to you, it's dynamic: meaning that it's malleable and exciting. It will take the shape of whatever you return in your query, and it will have properties and so on. You can assign events to it, you can create delegates on the fly. You can give it chocolate, and it will kiss you.

That's pretty much it. One thing I really like is the groovy DSL that Massive uses - it looks just like SQL. If you want, you can use this DSL to query the database:

```csharp
var table = new Products();
var productsThatILike = table.Query("SELECT ProductName, CategoryName FROM Products INNER JOIN Categories ON Categories.CategoryID = Products.CategoryID WHERE CategoryID = @0",5);
//get down!
```

Some of you might look at that and think it looks suspiciously like inline SQL. It *does* look sort of like it doesn't it! But I think it reads a bit better than Linq to SQL - it's a bit closer to the mark if you will. 

Inserts and Updates
-------------------
Massive is built on top of dynamics - so if you send an object to a table, it will get parsed into a query. If that object has a property on it that matches the primary key, Massive will think you want to update something. Unless you tell it specifically to update it.

You can send just about anything into the MassiveTransmoQueryfier and it will magically get turned into SQL:

```csharp
var table = new Products();
var poopy = new {ProductName = "Chicken Fingers"};
//update Product with ProductID = 12 to have a ProductName of "Chicken Fingers"
table.Update(poopy, 12);
```

This also works if you have a form on your web page with the name "ProductName" - then you submit it:

```csharp
var table = new Products();
//update Product with ProductID = 12 to have a ProductName of whatever was submitted via the form
table.Update(poopy, Request.Form);
```

Insert works the same way:

```csharp
//pretend we have a class like Products but it's called Categories
var table = new Categories();
//do it up - the inserted object will be returned from the query as expando 
var inserted = table.Insert(new {CategoryName = "Buck Fify Stuff", Description = "Things I like"});
// the new PK value is in the field specified as PrimaryKeyField in the constructor of Categories. 
var newID = inserted.CategoryID;
```

Yippee Skippy! Now we get to the fun part - and one of the reasons I had to spend 150 more lines of code on something you probably won't care about. What happens when we send a whole bunch of goodies to the database at once!

```csharp
var table = new Products();
//OH NO YOU DIDN't just pass in an integer inline without a parameter! 
//I think I might have... yes
var drinks = table.All("WHERE CategoryID = 8");
//what we get back here is an IEnumerable < ExpandoObject > - we can go to town
foreach(var item in drinks.ToArray()){
	//turn them into Haack Snacks
	item.CategoryID = 12;
}
//Let's update these in bulk, in a transaction shall we?
table.Save(drinks.ToArray());
```
	
Named Argument Query Syntax
-------------------
I recently added the ability to run more friendly queries using Named Arguments and C#4's Method-on-the-fly syntax. Originally this was trying to be like ActiveRecord, but I figured "C# is NOT Ruby, and Named Arguments can be a lot more clear". In addition, Mark Rendle's Simple.Data is already doing this so ... why duplicate things?

If your needs are more complicated - I would suggest just passing in your own SQL with Query().

```csharp
//important - must be dynamic
dynamic table = new Products();

var drinks = table.FindBy(CategoryID:8);
//what we get back here is an IEnumerable < ExpandoObject > - we can go to town
foreach(var item in drinks){
	Console.WriteLine(item.ProductName);
}
//returns the first item in the DB for category 8
var first = table.First(CategoryID:8);

//you dig it - the last as sorted by PK
var last = table.Last(CategoryID:8);

//you can order by whatever you like
var firstButReallyLast = table.First(CategoryID:8,OrderBy:"PK DESC");

//only want one column?
var price = table.First(CategoryID:8,Columns:"UnitPrice").UnitPrice;

//Multiple Criteria?
var items = table.Find(CategoryID:5, UnitPrice:100, OrderBy:"UnitPrice DESC");
```
	
Aggregates with Named Arguments
-------------------------------
You can do the same thing as above for aggregates:

```csharp
var sum = table.Sum(columns:"Price", CategoryID:5);
var avg = table.Avg(columns:"Price", CategoryID:3);
var min = table.Min(columns:"ID");
var max = table.Max(columns:"CreatedOn");
var count = table.Count();
```
	
Metadata
--------
If you find that you need to know information about your table - to generate some lovely things like ... whatever - just ask for the Schema property. This will query INFORMATION_SCHEMA for you, and you can take a look at DATA_TYPE, DEFAULT_VALUE, etc for whatever system you're running on.

In addition, if you want to generate an empty instance of a column - you can now ask for a "Prototype()" - which will return all the columns in your table with the defaults set for you (getdate(), raw values, newid(), etc).

Factory Constructor
-------------------
One thing that can be useful is to use Massive to just run a quick query. You can do that now by using "Open()" which is a static builder on DynamicModel:

```csharp
var db = Massive.DynamicModel.Open("myConnectionStringName");
```

You can execute whatever you like at that point.

Validations
-----------
One thing that's always needed when working with data is the ability to stop execution if something isn't right. Massive now has Validations, which are built with the Rails approach in mind:

```csharp
public class Productions:DynamicModel {
	public Productions():base("MyConnectionString","Productions","ID") {}
	public override void Validate(dynamic item) {
		ValidatesPresenceOf("Title");
		ValidatesNumericalityOf(item.Price);
		ValidateIsCurrency(item.Price);
		if (item.Price <= 0)
			Errors.Add("Price can't be negative");
	}
}
```

The idea here is that `Validate()` is called prior to Insert/Update. If it fails, an Error collection is populated and an InvalidOperationException is thrown. That simple. With each of the validations above, a message can be passed in.

CallBacks
---------
Need something to happen after Update/Insert/Delete? Need to halt before save? Massive has callbacks to let you do just that:

```csharp
public class Customers:DynamicModel {
	public Customers():base("MyConnectionString","Customers","ID") {}
	
	//Add the person to Highrise CRM when they're added to the system...
	public override void Inserted(dynamic item) {
		//send them to Highrise
		var svc = new HighRiseApi();
		svc.AddPerson(...);
	}
}
```

The callbacks you can use are:

 * Inserted
 * Updated
 * Deleted
 * BeforeDelete
 * BeforeSave



