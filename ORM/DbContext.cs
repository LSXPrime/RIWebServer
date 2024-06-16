using System.Data;
using System.Data.SQLite;
using System.Reflection;
using RIWebServer.Attributes.ORM;

namespace RIWebServer.ORM;

public abstract class DbContext(string connectionString)
{
    private readonly List<EntityChange> _changes = [];

    public IDbConnection CreateConnection()
    {
        return new SQLiteConnection(connectionString);
    }

    // Method to create tables if they don't exist (code-first approach)
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

    // Simple type mapping (can be extended with more types)
    private string GetSqlDataType(Type propertyType)
    {
        return propertyType switch
        {
            // Use the actual type names as constants
            not null when propertyType == typeof(int) => "INTEGER",
            not null when propertyType == typeof(string) => "TEXT",
            not null when propertyType == typeof(DateTime) => "TEXT",
            _ => throw new NotSupportedException($"Data type '{propertyType?.Name}' not supported.")
        };
    }

    // --- Helper methods for insert, update, and delete ---

    private void TrackChange(EntityChange change)
    {
        _changes.Add(change);
    }

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

    // --- Helper methods for insert, update, and delete ---
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

    private IEnumerable<PropertyInfo> GetPropertiesToPersist(Type entityType)
    {
        return entityType.GetProperties()
            .Where(p => p.CanWrite &&
                        !p.GetCustomAttributes<PrimaryKeyAttribute>().Any() &&
                        !p.GetCustomAttributes<NotMappedAttribute>().Any());
    }

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

    // --- Entity Change Tracking --- 
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

    // --- Methods to mark entities for change tracking --- 
    public void AddEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Added));
    }

    public void UpdateEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Modified));
    }

    public void DeleteEntity(object entity)
    {
        TrackChange(new EntityChange(entity, EntityState.Deleted));
    }
}