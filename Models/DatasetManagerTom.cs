using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;

namespace ProgrammingDatasets.Models {

  class DatasetManagerTom {

    public static Server server = new Server();

    // add static constructor to intialize connection
    static DatasetManagerTom() {
      string workspaceConnection = AppSettings.WorkspaceConnection;
      string accessToken = TokenManager.GetAccessToken(PowerBiPermissionScopes.TenantProvisioning);
      string connectStringUser = $"DataSource={workspaceConnection};Password={accessToken};";
      server.Connect(connectStringUser);
    }

    public static void DisplayDatabases() {
      foreach (Database database in server.Databases) {
        Console.WriteLine(database.Name);
        Console.WriteLine(" DatasetId: " + database.ID);
        Console.WriteLine(" CompatibilityLevel: " + database.CompatibilityLevel);
        Console.WriteLine(" CompatibilityMode: " + database.CompatibilityMode);
        Console.WriteLine(" EstimatedSize: " + database.EstimatedSize);
        Console.WriteLine(" LastUpdated: " + database.LastUpdate);
        Console.WriteLine(" LastProcessed: " + database.LastProcessed);
        Console.WriteLine(" LastSchemaUpdate: " + database.LastSchemaUpdate);

        Console.WriteLine();
      }
    }

    public static void DisplayDatabaseInfo(string Name) {

      Database database = server.Databases.GetByName(Name);

      Console.WriteLine(database.Name);
      Console.WriteLine(" DatasetId: " + database.ID);
      Console.WriteLine(" CompatibilityLevel: " + database.CompatibilityLevel);
      Console.WriteLine(" CompatibilityMode: " + database.CompatibilityMode);
      Console.WriteLine(" EstimatedSize: " + database.EstimatedSize);
      Console.WriteLine(" LastUpdated: " + database.LastUpdate);
      Console.WriteLine(" LastProcessed: " + database.LastProcessed);
      Console.WriteLine(" LastSchemaUpdate: " + database.LastSchemaUpdate);
    }

    public static void DisplayTableInfo(string DatabaseName, string TableName) {

      Database database = server.Databases.GetByName(DatabaseName);

      Table table = database.Model.Tables.Find("Sales");

      Console.WriteLine("Name: " + table.Name);
      Console.WriteLine(" ObjectType: " + table.ObjectType);
      Console.WriteLine(" # of Partitions: " + table.Partitions.Count);
      Console.WriteLine();

      Console.WriteLine("M code for table partition");
      Console.WriteLine("--------------------------");
      Partition partition = table.Partitions[0];
      var partitionSource = partition.Source as MPartitionSource;
      Console.WriteLine(partitionSource.Expression);
      Console.WriteLine("--------------------------");
      Console.WriteLine();
      Console.WriteLine();
    }

    public static void UpdateDateTimeFormatting(string DatabaseName) {
      Database database = server.Databases.GetByName(DatabaseName);
      Model datasetModel = database.Model;

      foreach (Table table in datasetModel.Tables) {
        foreach (Column column in table.Columns) {
          if (column.DataType == DataType.DateTime) {
            column.FormatString = "yyyy-MM-dd";
          }
        }
      }

      datasetModel.SaveChanges();
    }

    public static void UpdateTableQuery(string DatabaseName, string TableName) {

      Database database = server.Databases.GetByName(DatabaseName);
      Table table = database.Model.Tables.Find(TableName);
      Partition partition = table.Partitions[0];
      // get table partion as M partition
      var partitionSource = partition.Source as MPartitionSource;
      // get text for query
      string queryTemplate = Properties.Resources.ReplacementSalesQuery_m;
      string query = queryTemplate.Replace("@adlsStorageAccountUrl", AppSettings.adlsBlobAccount);
      query = query.Replace("@adlsContainerPath", AppSettings.adlsBlobAccount + AppSettings.adlsBlobContainer);
      query = query.Replace("@adlsFileName", AppSettings.adlsFileName);

      // update query text
      Console.WriteLine("Updating query with the following M code");
      Console.WriteLine();
      Console.WriteLine(query);
      partitionSource.Expression = query;
      database.Model.SaveChanges();

      // Use REST API to patch datasource credentals
      var workspace = DatasetManager.GetWorkspace(server.Name);
      var dataset = DatasetManager.GetDataset(workspace.Id, DatabaseName);
      DatasetManager.PatchAdlsCredentials(workspace.Id, dataset.Id);

      Console.WriteLine();
      Console.WriteLine();
      // refresh dataset using Excel file in ADLS
      Console.WriteLine("Query updated - running refresh operation");
      database.Model.RequestRefresh(RefreshType.DataOnly);
      database.Model.SaveChanges();
      Console.WriteLine("Dataset refresh complete");

    }

    public static void RefreshDatabaseModel(string Name) {
      Database database = server.Databases.GetByName(Name);
      database.Model.RequestRefresh(RefreshType.DataOnly);
      database.Model.SaveChanges();
    }

    public static Database CreateDatabase(string DatabaseName) {

      Console.WriteLine("Creating new dataset named " + DatabaseName);

      string newDatabaseName = server.Databases.GetNewName(DatabaseName);

      var database = new Database() {
        Name = newDatabaseName,
        ID = newDatabaseName,
        CompatibilityLevel = 1520,
        StorageEngineUsed = Microsoft.AnalysisServices.StorageEngineUsed.TabularMetadata,
        Model = new Model() {
          Name = DatabaseName + "-Model",
          Description = "A Demo Tabular data model with 1520 compatibility level."
        }
      };

      server.Databases.Add(database);
      database.Update(Microsoft.AnalysisServices.UpdateOptions.ExpandFull);

      return database;
    }

    public static Database CopyDatabase(string sourceDatabaseName, string DatabaseName) {

      Database sourceDatabase = server.Databases.GetByName(sourceDatabaseName);

      string newDatabaseName = server.Databases.GetNewName(DatabaseName);
      Database targetDatabase = CreateDatabase(newDatabaseName);
      sourceDatabase.Model.CopyTo(targetDatabase.Model);
      targetDatabase.Model.SaveChanges();

      targetDatabase.Model.RequestRefresh(RefreshType.Full);
      targetDatabase.Model.SaveChanges();

      return targetDatabase;
    }

    public static void CreateWingtipSalesModel(Database database) {

      Console.WriteLine("Creating Wingtip Sales Model");

      Model model = database.Model;

      Console.WriteLine(" Creating tables using TOM");

      Table tableCustomers = CreateCustomersTable();
      Table tableProducts = CreateProductsTable();
      Table tableSales = CreateSalesTable();
      Table tableCalendar = CreateCalendarTable();

      model.Tables.Add(tableCustomers);
      model.Tables.Add(tableProducts);
      model.Tables.Add(tableSales);
      model.Tables.Add(tableCalendar);

      Console.WriteLine(" Creating table releationships using TOM");

      model.Relationships.Add(new SingleColumnRelationship {
        Name = "Customers to Sales",
        ToColumn = tableCustomers.Columns["CustomerId"],
        ToCardinality = RelationshipEndCardinality.One,
        FromColumn = tableSales.Columns["CustomerId"],
        FromCardinality = RelationshipEndCardinality.Many
      });

      model.Relationships.Add(new SingleColumnRelationship {
        Name = "Products to Sales",
        ToColumn = tableProducts.Columns["ProductId"],
        ToCardinality = RelationshipEndCardinality.One,
        FromColumn = tableSales.Columns["ProductId"],
        FromCardinality = RelationshipEndCardinality.Many
      });

      model.Relationships.Add(new SingleColumnRelationship {
        Name = "Calendar to Sales",
        ToColumn = tableCalendar.Columns["DateKey"],
        ToCardinality = RelationshipEndCardinality.One,
        FromColumn = tableSales.Columns["DateKey"],
        FromCardinality = RelationshipEndCardinality.Many
      });

      Console.WriteLine(" Saving new database model using TOM");
      model.SaveChanges();

      Console.WriteLine(" Patching datasource credentials using Power BI REST API");


      // Use REST API to patch datasource credentals
      var workspace = DatasetManager.GetWorkspace(server.Name);
      var dataset = DatasetManager.GetDataset(workspace.Id, database.Name);
      DatasetManager.PatchSqlDatasourceCredentials(workspace.Id, dataset.Id, AppSettings.sqlUserName, AppSettings.sqlUserPassword);

      Console.WriteLine(" Refreshing dataset using TOM");

      model.RequestRefresh(RefreshType.Full);
      model.SaveChanges();

      Console.WriteLine(" Data model provisioning has completed");
      Console.WriteLine();
    }

    private static Table CreateCustomersTable() {

      Table customersTable = new Table() {
        Name = "Customers",
        Description = "Customers table",
        Partitions = {
            new Partition() {
                Name = "All Customers",
                Mode = ModeType.Import,
                Source = new MPartitionSource() {
                    Expression=Properties.Resources.CustomersQuery_m
                }
            }
        },
        Columns = {
            new DataColumn() { Name = "CustomerId", DataType = DataType.Int64, SourceColumn = "CustomerId", IsHidden=true },
            new DataColumn() { Name = "Customer", DataType = DataType.String, SourceColumn = "Customer" },
            new DataColumn() { Name = "State", DataType = DataType.String, SourceColumn = "State", DataCategory="StateOrProvince" },
            new DataColumn() { Name = "City", DataType = DataType.String, SourceColumn = "City", DataCategory="Place" },
            new DataColumn() { Name = "City Name", DataType = DataType.String, SourceColumn = "City Name" },
            new DataColumn() { Name = "Zipcode", DataType = DataType.String, SourceColumn = "Zipcode", DataCategory="PostalCode" },
            new DataColumn() { Name = "BirthDate", DataType = DataType.DateTime, SourceColumn = "BirthDate", IsHidden=true },
            new DataColumn() { Name = "Gender", DataType = DataType.String, SourceColumn = "Gender" },
            new DataColumn() { Name = "Customer Type", DataType = DataType.String, SourceColumn = "Customer Type" },
            new CalculatedColumn() { Name="Age", Expression = "Floor( (TODAY()-Customers[BirthDate])/365, 1)", IsHidden=true, SummarizeBy=AggregateFunction.Average },
            new CalculatedColumn() { Name="Age Group", Expression = Properties.Resources.CalculatedColumn_AgeGroup_dax },
            new CalculatedColumn() { Name="Sales Region", Expression = Properties.Resources.CalculatedColumn_SalesRegion_dax },
            new CalculatedColumn() { Name="SalesRegionSort", Expression = Properties.Resources.CalculatedColumn_SalesRegionSort_dax, DataType=DataType.Int64, IsHidden=true }
        }
      };

      customersTable.Columns["Sales Region"].SortByColumn = customersTable.Columns["SalesRegionSort"];

      customersTable.Hierarchies.Add(
        new Hierarchy() {
          Name = "Customer Geography",
          Levels = {
              new Level() { Ordinal=0, Name="Sales Region", Column=customersTable.Columns["Sales Region"]  },
              new Level() { Ordinal=1, Name="State", Column=customersTable.Columns["State"] },
              new Level() { Ordinal=2, Name="City", Column=customersTable.Columns["City"] },
              new Level() { Ordinal=3, Name="Zipcode", Column=customersTable.Columns["Zipcode"] }
            }
        });

      return customersTable;
    }

    private static Table CreateProductsTable() {

      Table productsTable = new Table() {
        Name = "Products",
        Description = "Products table",
        Partitions = {
          new Partition() {
            Name = "All Products",
            Mode = ModeType.Import,
            Source = new MPartitionSource() {
              Expression = Properties.Resources.ProductQuery_m
            }
          }
        },
        Columns = {
          new DataColumn() { Name = "ProductId", DataType = DataType.Int64, SourceColumn = "ProductId", IsHidden = true },
          new DataColumn() { Name = "Product", DataType = DataType.String, SourceColumn = "Product" },
          new DataColumn() { Name = "Description", DataType = DataType.String, SourceColumn = "Description" },
          new DataColumn() { Name = "Category", DataType = DataType.String, SourceColumn = "Category" },
          new DataColumn() { Name = "Subcategory", DataType = DataType.String, SourceColumn = "Subcategory" },
          new DataColumn() { Name = "Product Image", DataType = DataType.String, SourceColumn = "ProductImageUrl", DataCategory = "ImageUrl" }
        }
      };

      productsTable.Hierarchies.Add(
        new Hierarchy() {
          Name = "Product Category",
          Levels = {
              new Level() { Ordinal=0, Name="Category", Column=productsTable.Columns["Category"]  },
              new Level() { Ordinal=1, Name="Subcategory", Column=productsTable.Columns["Subcategory"] },
              new Level() { Ordinal=2, Name="Product", Column=productsTable.Columns["Product"] }
            }
        });

      return productsTable;
    }

    private static Table CreateSalesTable() {

      return new Table() {
        Name = "Sales",
        Description = "Sales table",
        Partitions = {
            new Partition() {
                Name = "All Sales",
                Mode = ModeType.Import,
                Source = new MPartitionSource() {
                    Expression=Properties.Resources.SalesQuery_m
                }
            }
        },
        Columns = {
            new DataColumn() { Name = "Id", DataType = DataType.Int64, SourceColumn = "Id", IsHidden=true },
            new DataColumn() { Name = "Quantity", DataType = DataType.Int64, SourceColumn = "Quantity", IsHidden=true  },
            new DataColumn() { Name = "SalesAmount", DataType = DataType.Decimal, SourceColumn = "SalesAmount", IsHidden=true  },
            new DataColumn() { Name = "InvoiceId", DataType = DataType.Int64, SourceColumn = "InvoiceId", IsHidden=true  },
            new DataColumn() { Name = "CustomerId", DataType = DataType.Int64, SourceColumn = "CustomerId", IsHidden=true  },
            new DataColumn() { Name = "ProductId", DataType = DataType.Int64, SourceColumn = "ProductId", IsHidden=true  },
            new DataColumn() { Name = "InvoiceDate", DataType = DataType.DateTime, SourceColumn = "InvoiceDate", IsHidden=true },
            new CalculatedColumn() { Name="DateKey", DataType = DataType.Int64, IsHidden=true, Expression="Year([InvoiceDate])*10000 + Month([InvoiceDate])*100 + Day([InvoiceDate])" }
        },
        Measures = {
          new Measure { Name = "Sales Revenue", Expression = "Sum(Sales[SalesAmount])", FormatString=@"\$#,0;(\$#,0);\$#,0" },
          new Measure { Name = "Units Sold", Expression = "Sum(Sales[Quantity])", FormatString="#,0" },
          new Measure { Name = "Customer Count", Expression = "CountRows(Customers)", FormatString="#,0"  }
        }
      };
    }

    private static Table CreateCalendarTable() {

      Table calendarTable = new Table {
        Name = "Calendar",
        Partitions = {
          new Partition {
            Source = new CalculatedPartitionSource {
              Expression = Properties.Resources.CalculatedTable_Calendar_dax,
            }
          }
        },
        Columns = {
          new DataColumn() { Name = "Date", DataType = DataType.DateTime, SourceColumn = "Date", FormatString="MM/dd/yyyy" },
          new CalculatedColumn() { Name = "DateKey", DataType = DataType.Int64, IsHidden = true ,Expression = "Year([Date])*10000 + Month([Date])*100 + Day([Date])" },
          new CalculatedColumn() { Name = "Year", DataType = DataType.Int64, Expression = "Year([Date])", SummarizeBy=AggregateFunction.None },
          new CalculatedColumn() { Name = "Quarter", DataType = DataType.String, Expression = @"Year([Date]) & ""-Q"" & FORMAT([Date], ""q"")" },
          new CalculatedColumn() { Name = "Month", DataType = DataType.String, Expression = @"FORMAT([Date], ""MMM yyyy"")", },
          new CalculatedColumn() { Name = "MonthSort", DataType = DataType.String, Expression = @"Format([Date], ""yyyy-MM"")", IsHidden = true },
          new CalculatedColumn() { Name = "Month in Year", DataType = DataType.String, Expression = @"FORMAT([Date], ""MMM"")" },
          new CalculatedColumn() { Name = "MonthInYearSort", DataType = DataType.Int64, Expression = "MONTH([Date])", IsHidden = true },
          new CalculatedColumn() { Name = "Day of Week", DataType = DataType.String, Expression = @"FORMAT([Date], ""ddd"")" },
          new CalculatedColumn() { Name = "DayOfWeekSort", DataType = DataType.Int64, Expression = "WEEKDAY([Date], 2)", IsHidden = true }
        }
      };

      calendarTable.Columns["Month"].SortByColumn = calendarTable.Columns["MonthSort"];
      calendarTable.Columns["Month in Year"].SortByColumn = calendarTable.Columns["MonthInYearSort"];
      calendarTable.Columns["Day of Week"].SortByColumn = calendarTable.Columns["DayOfWeekSort"];

      calendarTable.Hierarchies.Add(
          new Hierarchy() {
            Name = "Calendar Drilldown",
            Levels = {
                  new Level() { Ordinal=0, Name="Year", Column=calendarTable.Columns["Year"]  },
                  new Level() { Ordinal=1, Name="Quarter", Column=calendarTable.Columns["Quarter"] },
                  new Level() { Ordinal=2, Name="Month", Column=calendarTable.Columns["Month"] }
              }
          });

      return calendarTable;
    }

  }
}
