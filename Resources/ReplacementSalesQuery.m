let
    Source = AzureStorage.Blobs("@adlsStorageAccountUrl"),
    exceldata1 = Source{[Name="exceldata"]}[Data],
    ExcelFile = exceldata1{[#"Folder Path"="@adlsContainerPath",Name="@adlsFileName"]}[Content],
    ImportedExcel = Excel.Workbook(ExcelFile),
    SalesTable = ImportedExcel{[Item="Sales",Kind="Table"]}[Data],
    Output = Table.TransformColumnTypes(SalesTable,{{"Sales", Currency.Type}})
in
    Output