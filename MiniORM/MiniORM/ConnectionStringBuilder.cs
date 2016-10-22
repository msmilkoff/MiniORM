namespace MiniORM
{
    using System.Data.SqlClient;

    /// <summary>
    /// Builds a conenction string via a given DB name, assuming the database is local
    /// and uses windows credentials to log in.
    /// </summary>
    public class ConnectionStringBuilder
    {
        private SqlConnectionStringBuilder builder;
        private string connectionString;

        public ConnectionStringBuilder(string databaseName)
        {
            this.builder = new SqlConnectionStringBuilder();
            this.builder["Data Source"] = "(local)";
            this.builder["Integrated Security"] = true;
            this.builder["Connect Timeout"] = 1000;
            this.builder["Trusted_Connection"] = true;
            this.builder["Initial Catalog"] = databaseName;

            this.connectionString = builder.ToString();
        }

        public string ConnectionString => this.connectionString;
    }
}