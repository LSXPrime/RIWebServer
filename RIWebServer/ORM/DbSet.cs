using System.Collections;
using System.Data;
using System.Reflection;
using RIWebServer.Attributes.ORM;

namespace RIWebServer.ORM;

public class DbSet<TEntity>(DbContext dbContext)
    where TEntity : class, new()
{
    private readonly string _tableName = typeof(TEntity).Name;

    public TEntity? GetById(int id)
    {
        using var connection = dbContext.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_tableName} WHERE Id = @Id";
        var param = command.CreateParameter();
        param.ParameterName = "@Id";
        param.Value = id;
        command.Parameters.Add(param);

        using var reader = command.ExecuteReader();
        return MapReaderToList<TEntity>(reader).FirstOrDefault();
    }

    public List<TEntity> GetAll()
    {
        using var connection = dbContext.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {_tableName}";

        using var reader = command.ExecuteReader();
        return MapReaderToList<TEntity>(reader).ToList();
    }

    public List<TEntity> GetAllWithRelated()
    {
        var entities = GetAll();
        var navigationProperties = typeof(TEntity).GetProperties()
            .Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null && p.CanWrite);

        foreach (var navProperty in navigationProperties)
        {
            var foreignKeyProperty = navProperty.Name;
            var relatedTable = navProperty.GetCustomAttribute<ForeignKeyAttribute>()!.RelatedTable;
            
            var relatedIds = entities.Select(e => (int)e.GetType().GetProperty(foreignKeyProperty)!.GetValue(e)!)
                .Distinct().ToList();
            
            var relatedEntities = GetRelatedEntities(relatedTable, relatedIds);

            foreach (var entity in entities)
            {
                var relatedId = (int)entity.GetType().GetProperty(foreignKeyProperty)!.GetValue(entity)!;
                var mainEntity =
                    relatedEntities.FirstOrDefault(re => dbContext.GetPrimaryKeyValue(re).Equals(relatedId));
                var relatedEntity = mainEntity?.GetType().GetProperties()
                    .FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                    ?.GetValue(mainEntity)!;

                navProperty.SetValue(entity, relatedEntity);
            }
        }
        return entities;
    }

    private List<object> GetRelatedEntities(string relatedTable, List<int> relatedIds)
    {
        try
        {
            var relatedDbSetProperty = dbContext.GetType().GetProperty(relatedTable + "s")!;
            var relatedDbSet = relatedDbSetProperty.GetValue(dbContext);

            var getAllMethod = relatedDbSetProperty.PropertyType.GetMethod("GetAll");
            var relatedEntities = (IList)getAllMethod!.Invoke(relatedDbSet, null)!;
            
            return relatedEntities.Cast<object>()
                .Where(re => relatedIds.Contains((int)dbContext.GetPrimaryKeyValue(re)))
                .ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void Add(TEntity entity)
    {
        dbContext.AddEntity(entity);
    }

    public void Update(TEntity entity)
    {
        dbContext.UpdateEntity(entity);
    }

    public void Delete(int id)
    {
        var entity = GetById(id);
        if (entity != null)
        {
            dbContext.DeleteEntity(entity);
        }
    }

    // Helper method to map data reader to a list of entities
    private IEnumerable<TMap> MapReaderToList<TMap>(IDataReader reader) where TMap : class, new()
    {
        var entityType = typeof(TMap);
        var properties = entityType.GetProperties().Where(p => p.CanWrite).ToArray();

        while (reader.Read())
        {
            var entity = new TMap();
            foreach (var property in properties)
            {
                var columnName = property.Name;
                var ordinal = reader.GetOrdinal(columnName);

                if (!reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);

                    if (property.PropertyType == typeof(DateTime) && value is string dateTimeString)
                    {
                        value = DateTime.Parse(dateTimeString);
                    }

                    if (property.PropertyType == typeof(int) && value is long longValue)
                    {
                        value = (int)longValue;
                    }

                    property.SetValue(entity, value);
                }
            }

            yield return entity;
        }
    }
}