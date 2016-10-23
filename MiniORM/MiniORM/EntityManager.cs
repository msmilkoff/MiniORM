namespace MiniORM
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Attributes;

    public class EntityManager : IDbContext
    {
        private SqlConnection connection;
        private string connectionString;
        private bool isCodeFirst;

        public EntityManager(string connectionString, bool isCodeFirst)
        {
            this.connectionString = connectionString;
            this.isCodeFirst = isCodeFirst;
        }

        public IEnumerable<T> FindAll<T>(string where)
        {
            var result = new List<T>();

            var query = new StringBuilder($"SELECT * FROM {this.GetTableName(typeof(T))} ");
            if (where != null)
            {
                query.Append($" WHERE {where.Trim()}");
            }

            using (this.connection = new SqlConnection(this.connectionString))
            {
                var cmd = new SqlCommand(query.ToString(), this.connection);
                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        T entity = CreateEntity<T>(reader);
                        result.Add(entity);
                    }
                }
            }

            return result;
        }

        public IEnumerable<T> FindAll<T>()
        {
            return this.FindAll <T>(null);
        }

        public T FindById<T>(int id)
        {
            T result = default(T);

            using (this.connection = new SqlConnection(this.connectionString))
            {
                string commandText = $"SELECT * FROM {this.GetTableName(typeof(T))} WHERE Id = @Id ";
                var getEntityCommand = new SqlCommand(commandText, this.connection);
                getEntityCommand.Parameters.AddWithValue("@Id", id);

                this.connection.Open();
                using (var reader = getEntityCommand.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        throw new InvalidOperationException("No entity was found with id " + id);
                    }

                    reader.Read();
                    result = CreateEntity<T>(reader);
                }
            }

            return result;
        }

        public T FindFirst<T>()
        {
            return this.FindFirst<T>(null);
        }

        public T FindFirst<T>(string where)
        {
            T result = default(T);

            string query = $"SELECT TOP 1 * FROM {this.GetTableName(typeof(T))} ";
            if (where != null)
            {
                query += $" WHERE {where}";
            }

            using (this.connection = new SqlConnection(this.connectionString))
            {
                var cmd = new SqlCommand(query, this.connection);
                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = this.CreateEntity<T>(reader);
                    }
                }
            }

            return result;
        }

        public bool Persist(object entity)
        {
            if (entity == null)
            {
                return false;
            }

            Type typeOfEntity = entity.GetType();
            if (this.isCodeFirst && !this.TableExists(typeOfEntity))
            {
                this.CreateTable(typeOfEntity);
            }

            FieldInfo primary = this.GetId(typeOfEntity);
            var value = primary.GetValue(entity);

            if (value == null || (int)value <= 0)
            {
                return this.Insert(entity, primary);
            }

            return this.Update(entity, primary);
        }

        private T CreateEntity<T>(SqlDataReader reader)
        {
            var originalValues = new object[reader.FieldCount];
            reader.GetValues(originalValues);

            var values = new object[reader.FieldCount - 1];
            Array.Copy(originalValues, 1, values, 0, reader.FieldCount - 1);
            var types = new Type[values.Length];
            for (int i = 0; i < types.Length; i++)
            {
                types[i] = values[i].GetType();
            }

            T entity = (T)typeof(T).GetConstructor(types).Invoke(values);
            typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.IsDefined(typeof(IdAttribute)))
                .SetValue(entity, originalValues[0]);

            return entity;
        }

        private bool Update<T>(T entity, FieldInfo primary)
        {
            string updateStatement = this.GetUpdateStatement(entity, primary);
            int rowsAffected;

            using (this.connection = new SqlConnection(this.connectionString))
            {
                var updateCommand = new SqlCommand(updateStatement, this.connection);
                connection.Open();
                rowsAffected = (int)updateCommand.ExecuteScalar();

                return rowsAffected > 0;
            }
        }

        private string GetUpdateStatement<T>(T entity, FieldInfo primary)
        {
            var fields = entity.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            var updateStatement = new StringBuilder("UPDATE " + this.GetTableName(entity.GetType()) + " SET ");
            var filterClause = new StringBuilder(" WHERE ");
            foreach (var field in fields)
            {
                if (field == primary)
                {
                    filterClause.Append($"[{this.GetFieldName(field)}] = ");
                    filterClause.Append($"'{field.GetValue(entity).ToString()}'");
                    continue;
                }

                if (field.GetValue(entity) != null)
                {
                    updateStatement.Append($"[{this.GetFieldName(field)}] = ");
                    string currentValue = string.Empty;
                    if (field.FieldType == typeof(DateTime))
                    {
                        DateTime date = (DateTime)field.GetValue(entity);
                        currentValue += date.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        currentValue = field.GetValue(entity).ToString();
                    }

                    updateStatement.Append($"'{currentValue}', ");
                }
            }

            updateStatement.Remove(updateStatement.Length - 2, 2);
            updateStatement.Append(filterClause);

            return updateStatement.ToString();
        }

        private bool Insert<T>(T entity, FieldInfo primary)
        {
            string insertStatement = this.GetInsertStatemement(entity, primary);
            int rowsAffected = 0;

            using (this.connection = new SqlConnection(this.connectionString))
            {
                var insertCommand = new SqlCommand(insertStatement, this.connection);
                connection.Open();
                rowsAffected = insertCommand.ExecuteNonQuery();
            }
            
            // Set the entity's Id field to the Id of the last entry in the Table, which is auto-generated.
            using (this.connection = new SqlConnection(this.connectionString))
            {
                var getIdOfLastEntryCommand = new SqlCommand(
                    $"SELECT Max(Id) from {this.GetTableName(entity.GetType())}", this.connection);
                connection.Open();
                int id = (int)getIdOfLastEntryCommand.ExecuteScalar();
                this.GetId(entity.GetType()).SetValue(entity, id);

                return rowsAffected > 0;
            }
        }

        private string GetInsertStatemement<T>(T entity, FieldInfo primary)
        {
            var columnNames = new StringBuilder();
            var values = new StringBuilder();

            var fields = entity.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field == primary)
                {
                    continue;
                }

                columnNames.Append($"[{this.GetFieldName(field)}], ");

                if (field.FieldType == typeof(DateTime))
                {
                    DateTime date = (DateTime)field.GetValue(entity);
                    values.Append($"'{date.ToString("yyyy-MM-dd HH:mm:ss")}', ");
                }
                else
                {
                    values.Append($"'{field.GetValue(entity).ToString()}', ");
                }
            }

            columnNames.Remove(columnNames.Length - 2, 2);
            values.Remove(values.Length - 2, 2);

            string insertStatement = $"INSERT INTO {this.GetTableName(entity.GetType())} " +
                                $"({columnNames})" +
                                $" VALUES ({values})";

            return insertStatement;
        }

        private FieldInfo GetId(Type entity)
        {
            var field = entity.GetFields()
                .Where(f => f.IsDefined(typeof(IdAttribute)))
                .First();
            if (field == null)
            {
                throw new InvalidOperationException("Cannot operate with entity without primary key.");
            }

            return field;
        }

        private string GetTableName(Type entity)
        {
            string tableName = entity.GetCustomAttribute<EntityAttribute>()?.TableName;

            if (string.IsNullOrEmpty(tableName))
            {
                return entity.Name;
            }

            return tableName;
        }

        private string GetFieldName(FieldInfo field)
        {
            string fieldName = field.GetCustomAttribute<ColumnAttribute>()?.Name;
            if (string.IsNullOrEmpty(fieldName))
            {
                return field.Name;
            }

            return fieldName;
        }

        private string GetCreateTableStatement(Type table)
        {
            var createSQL = new StringBuilder($"CREATE TABLE {this.GetTableName(table)} (");
            createSQL.Append("Id INT IDENTITY PRIMARY KEY, ");

            var columnNames = table.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.IsDefined(typeof(ColumnAttribute)))
                .Select(f => this.GetFieldName(f))
                .ToArray();

            var fields = table.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.IsDefined(typeof(ColumnAttribute)))
                .ToArray();

            for (int i = 0; i < fields.Length; i++)
            {
                createSQL.Append($"{columnNames[i]} {this.ToDbType(fields[i].FieldType)}, ");
            }

            // Remove comma and space at the end of the last column declaration.
            createSQL.Remove(createSQL.Length - 2, 2);
            createSQL.Append(")");

            return createSQL.ToString();
        }

        private string ToDbType(Type fieldType)
        {
            switch (fieldType.Name)
            {
                case "Int32":
                    return "INT";
                case "String":
                    return "VARCHAR(100)";
                case "Boolean":
                    return "BIT";
                case "DateTime":
                    return "DATETIME";
                default:
                    throw new NotSupportedException($"Type {fieldType.Name} is not yet supported.");
            }
        }

        private void CreateTable(Type table)
        {
            string createTableStatement = this.GetCreateTableStatement(table);

            using (this.connection = new SqlConnection(this.connectionString))
            {
                var createCommand = new SqlCommand(createTableStatement, this.connection);
                this.connection.Open();
                createCommand.ExecuteNonQuery();
            }
        }

        private bool TableExists(Type table)
        {
            using (this.connection = new SqlConnection(this.connectionString))
            {
                var getTablesCountQuery = 
                    $@"SELECT COUNT(name) 
                         FROM sys.sysobjects 
                        WHERE [Name] = '{this.GetTableName(table)}' 
                          AND [xtype] = U'";

                var getTablesCountCmd = new SqlCommand(getTablesCountQuery, this.connection);
                connection.Open();
                int tablesCount = (int)getTablesCountCmd.ExecuteScalar();

                return tablesCount > 0;
            }
        }
    }
}