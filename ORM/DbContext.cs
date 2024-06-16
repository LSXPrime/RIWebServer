using System.Data;
using System.Data.SQLite;
using System.Reflection;
using RIWebServer.Attributes.ORM;

namespace RIWebServer.ORM;

public abstract class DbContext(string connectionString)
{
    private readonly List<EntityChange> _changes = [];

    /// <summary>
    /// Creates a new instance of the IDbConnection class using the provided connection string.
    /// </summary>
    /// <returns>An instance of the SQLiteConnection class.</returns>
    public IDbConnection CreateConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    /// <summary>
    /// Ensures that the database is created if it does not already exist.
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        using var connection = CreateConnection();
        connection.Open();

        var dbSetProperties = this.GetType().GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

        foreach (var property in dbSetProperties)
        {
            var entityType = property.PropertyType.GetGenericArguments()[0];
            CreateTableIfNotExists(connection, entityType);
        }
    }

    /// <summary>
    /// Creates a table in the database if it does not already exist.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="entityType">The type of the entity.</param>
    private void CreateTableIfNotExists(IDbConnection connection, Type entityType)
    {
        var tableName = entityType.Name;
        var properties = entityType.GetProperties();
        var primaryKeyProperty = properties.FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null);
        var createTableSql = $"CREATE TABLE IF NOT EXISTS {tableName} (";

        foreach (var property in properties)
        {
            if (property.GetCustomAttributes<NotMappedAttribute>().Any())
                continue;
            
            var columnName = property.Name;
            var sqlDataType = GetSqlDataType(property.PropertyType);
            var isPrimaryKey = property == primaryKeyProperty;

            createTableSql += $"{columnName} {sqlDataType}";

            if (isPrimaryKey)
            {
                createTableSql += " PRIMARY KEY AUTOINCREMENT";
            }
            else
            {
                var foreignKeyAttribute = property.GetCustomAttribute<ForeignKeyAttribute>();
                if (foreignKeyAttribute != null)
                {
                    createTableSql += $" REFERENCES {foreignKeyAttribute.RelatedTable}(Id)";
                }
            }

            createTableSql += ", ";
        }

        createTableSql = createTableSql.TrimEnd(',', ' ') + ")";

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    
    /// <summary>
    /// Returns the SQL data type for a given property type.
    /// </summary>
    /// <param name="propertyType">The property type.</param>
    /// <returns>The corresponding SQL data type.</returns>
    /// <exception cref="NotSupportedException">Thrown if the data type is not supported.</exception>
    private string GetSqlDataType(Type propertyType)
    {
        return propertyType switch
        {
            not null when propertyType == typeof(int) => "INTEGER",
            not null when propertyType == typeof(string) => "TEXT",
            not null when propertyType == typeof(DateTime) => "TEXT",
            _ => throw new NotSupportedException($"Data type '{propertyType?.Name}' not supported.")
        };
    }
    
    /// <summary>
    /// Adds a <see cref="EntityChange"/> to the internal list of changes.
    /// </summary>
    /// <param name="change">The <see cref="EntityChange"/> to add.</param>
    private void TrackChange(EntityChange change)
    {
        _changes.Add(change);
    }

    /// <summary>
    /// Saves changes made to the database by executing the appropriate insert, update, or delete queries.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the changes were successfully saved, <c>false</c> otherwise.
    /// </returns>
    public bool SaveChanges()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var change in _changes)
            {
                switch (change.State)
                {
                    case EntityState.Added:
                        InsertEntity(connection, change.Entity);
                        break;
                    case EntityState.Modified:
                        UpdateEntity(connection, change.Entity);
                        break;
                    case EntityState.Deleted:
                        DeleteEntity(connection, change.Entity);
                        break;
                }
            }

            transaction.Commit();
            _changes.Clear();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving changes: {ex.Message}");
            transaction.Rollback();
            return false;
        }
    }

    /// <summary>
    /// Inserts an entity into the specified database connection.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to be inserted.</param>
    /// <exception cref="Exception">If there is an error executing the insert statement.</exception>
    private void InsertEntity(IDbConnection connection, object entity)
    {
        var entityType = entity.GetType();
        var tableName = entityType.Name;
        var properties = GetPropertiesToPersist(entityType).ToArray();

        var insertSql = $"INSERT INTO {tableName} ({string.Join(", ", properties.Select(p => p.Name))}) " +
                        $"VALUES ({string.Join(", ", properties.Select(p => "@" + p.Name))})";

        using var command = connection.CreateCommand();
        command.CommandText = insertSql;
        AddParameters(command, entity, properties);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates an entity in the database table based on the provided entity and connection.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to be updated.</param>
    private void UpdateEntity(IDbConnection connection, object entity)
    {
        var entityType = entity.GetType();
        var tableName = entityType.Name;
        var properties = GetPropertiesToPersist(entityType).ToArray();

        var updateSql =
            $"UPDATE {tableName} SET {string.Join(", ", properties.Select(p => p.Name + " = @" + p.Name))} " +
            $"WHERE Id = @Id";

        using var command = connection.CreateCommand();
        command.CommandText = updateSql;
        AddParameters(command, entity, properties);
        var param = command.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = GetPrimaryKeyValue(entity);
        command.Parameters.Add(param);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Deletes an entity from the database table based on the provided entity and connection.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <param name="entity">The entity to be deleted.</param>
    private void DeleteEntity(IDbConnection connection, object entity)
    {
        var entityType = entity.GetType();
        var tableName = entityType.Name;

        var deleteSql = $"DELETE FROM {tableName} WHERE Id = @Id";

        using var command = connection.CreateCommand();
        command.CommandText = deleteSql;
        var param = command.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = GetPrimaryKeyValue(entity);
        command.Parameters.Add(param);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Retrieves the primary key value from the given entity.
    /// </summary>
    /// <param name="entity">The entity to retrieve the primary key value from.</param>
    /// <returns>The primary key value of the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no primary key property is found.</exception>
    public object GetPrimaryKeyValue(object entity)
    {
        var primaryKeyProperty = entity.GetType().GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null);

        if (primaryKeyProperty != null)
            return primaryKeyProperty.GetValue(entity)!;

        if (entity.GetType().GetProperty("Id")!.GetValue(entity) is int id)
            return id;

        throw new InvalidOperationException("No primary key property found.");
    }

    /// <summary>
    /// Retrieves the properties of the specified entity type that are to be persisted.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <returns>An enumerable collection of PropertyInfo objects representing the properties to persist.</returns>
    private IEnumerable<PropertyInfo> GetPropertiesToPersist(Type entityType)
    {
        return entityType.GetProperties()
            .Where(p => p.CanWrite &&
                        !p.GetCustomAttributes<PrimaryKeyAttribute>().Any() &&
                        !p.GetCustomAttributes<NotMappedAttribute>().Any());
    }

    /// <summary>
    /// Adds the parameters of the specified entity to the specified command.
    /// </summary>
    /// <param name="command">The command to add the parameters to.</param>
    /// <param name="entity">The entity to add the parameters for.</param>
    /// <param name="properties">The properties of the entity to add the parameters for.</param>
    private void AddParameters(IDbCommand command, object entity, IEnumerable<PropertyInfo> properties)
    {
        foreach (var property in properties)
        {
            var value = property.GetValue(entity);
            var param = command.CreateParameter();
            param.ParameterName = $"@{property.Name}";
            param.Value = value;
            command.Parameters.Add(param);
        }
    }

    private enum EntityState
    {
        Added,
        Modified,
        Deleted
    }

    private class EntityChange(object entity, EntityState state)
    {
        public object Entity { get; } = entity;
        public EntityState State { get; } = state;
    }

    /// <summary>
    /// Adds an entity to the internal list of changes.
    /// </summary>
    /// <param name="entity"></param>
    public void AddEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Added));
    }
    
    /// <summary>
    /// Modifies an entity in the internal list of changes.
    /// </summary>
    /// <param name="entity"></param>
    public void UpdateEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Modified));
    }

    /// <summary>
    /// Deletes an entity from the internal list of changes.
    /// </summary>
    /// <param name="entity"></param>
    public void DeleteEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Deleted));
    }
}