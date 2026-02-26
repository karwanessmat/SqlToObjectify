using SqlToObjectify.Test;

//var bookifyHelper = new BookifyDbContextHelper();

//await bookifyHelper.Invoice();

//var sqlObjectDbContextHelper = new SqlObjectDbContextHelper();
//await sqlObjectDbContextHelper.SelectSqlQueryListAsync();


var sqlObjectDbContextHelper = new SqlObjectDbContextHelper();
await sqlObjectDbContextHelper.SelectStoredProcedureListAsync();
//await sqlObjectDbContextHelper.sp_GetAllProjectRecordsListAsync();
//await sqlObjectDbContextHelper.SelectSqlQuery_GetAllProjectRecordsListAsync();
//await sqlObjectDbContextHelper.sp_Sparda_SelectStoredProcedureListAsync();