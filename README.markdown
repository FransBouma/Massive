Massive is a Single File Database Lover. Move over Bacon - Taste is got a new friend in .NET Land
=================================================================================================


I'm sharing this with the world because we need another way to access data - don't you think? Truthfully - I wanted to see if I could flex the C# 4 stuff and
run up data access with a single file. This is ready to roll and weighs in at a lovely 346 lines of code. Most of which is comments.

How To Install It?
------------------
Drop the code file into your app and change it as you wish. 

How Do You Use It?
------------------
Massive is a "wrapper" for your DB tables and uses System.Dynamic extensively. If you try to use this with C# 3.5 or below, it will explode and you will be sad. Me too honestly - I like how this doesn't require any DLLs other than what's in the GAC. Yippee.

 * Get a Database. Northwind will work nicely. Add a connection to your database in your web.config (or app.config). Don't forget the providerName! If you don't know what that is - just add providerName = 'System.Data.SqlClient' right after the whole connectionString stuff.
 * Create a class that wraps a table. You can call it whatever you like, but if you want to be cool just name it the same as your table.
 * Query away and have fun

Code Please
-----------
Let's say we have a table named "Products". You create a class like this:
    
	public class Products:DynamicModel {
        	//you don't have to specify the connection - Massive will use the first one it finds in your config
		public Products():base("northwind") {
            		PrimaryKeyField = "ProductID";
        	}

    	}

You could also just instantiate it inline, as needed:
	var tbl = new DynamicModel("northwind", tableName:"Products", primaryKeyField:"ProductID");


Now you can query thus:
	var table = new Products();
	//grab all the products
	var products = table.All();
	//just grab from category 4. This uses named parameters
	var productsFour = table.All(columns: "ProductName as Name", where: "WHERE categoryID=@0",args: 4);

You can also run ad-hoc queries as needed:
	var result = tbl.Query("SELECT * FROM Categories");

This will pull categories and enumerate the results - streaming them as opposed to bulk-fetching them (thanks to Jeroen Haegebaert for the code). If you need to run a Fetch - you can:
	var result = tbl.Fetch("SELECT * FROM Categories");

If you want to have a Paged result set - you can:
	var result = tbl.Paged(where: "UnitPrice > 20", currentPage:2, pageSize: 20);
In this example, ALL of the arguments are optional and default to reasonable values. CurrentPage defaults to 1, pageSize defaults to 20, where defaults to nothing.

What you get back is IEnumerable < ExpandoObject > - meaning that it's malleable and exciting. It will take the shape of whatever you return in your query, and it will have properties and so on. You can assign events to it, you can create delegates on the fly. You can give it chocolate, and it will kiss you.

That's pretty much it. One thing I really like is the groovy DSL that Massive uses - it looks just like SQL. If you want, you can use this DSL to query the database:
	var table = new Products();
	var productsThatILike = table.Query("SELECT ProductName, CategoryName FROM Products INNER JOIN Categories ON Categories.CategoryID = Products.CategoryID WHERE CategoryID = @0",5);
	//get down!

Some of you might look at that and think it looks suspiciously like inline SQL. It *does* look sort of like it doesn't it! But I think it reads a bit better than Linq to SQL - it's a bit closer to the mark if you will. 

Inserts and Updates
-------------------
Massive is built on top of dynamics - so if you send an object to a table, it will get parsed into a query. If that object has a property on it that matches the primary key, Massive will think you want to update something. Unless you tell it specifically to update it.

You can send just about anything into the MassiveTransmoQueryfier and it will magically get turned into SQL:
	var table = new Products();
	var poopy = new {ProductName = "Chicken Fingers"};
	//update Product with ProductID = 12 to have a ProductName of "Chicken Fingers"
	table.Update(poopy, 12);

This also works if you have a form on your web page with the name "ProductName" - then you submit it:
	var table = new Products();
	//update Product with ProductID = 12 to have a ProductName of whatever was submitted via the form
	table.Update(poopy, Request.Form);

Insert works the same way:
	//pretend we have a class like Products but it's called Categories
	var table = new Categories();
	//do it up - the new ID will be returned from the query
	var newID = table.Insert(new {CategoryName = "Buck Fify Stuff", Description = "Things I like"});

Yippee Skippy! Now we get to the fun part - and one of the reasons I had to spend 150 more lines of code on something you probably won't care about. What happens when we send a whole bunch of goodies to the database at once!
	var table = new Products();
	//OH NO YOU DIDN't just pass in an integer inline without a parameter! 
	//I think I might have... yes
	var drinks = table.All("WHERE CategoryID = 8");
	//what we get back here is an IEnumerable < ExpandoObject > - we can go to town
	foreach(var item in drinks){
		//turn them into Haack Snacks
		item.CategoryID = 12;
	}
	//Let's update these in bulk, in a transaction shall we?
	table.Save(drinks);