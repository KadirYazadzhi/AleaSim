using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace AleaSim.Domain.Services;

public class RedisService : IRedisService {
    private readonly ConnectionMultiplexer _redis;

    public RedisService(IConfiguration configuration) {
        var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _redis = ConnectionMultiplexer.Connect(connectionString);
    }

    public IDatabase GetDatabase() {
        return _redis.GetDatabase();
    }

    public ISubscriber GetSubscriber() {
        return _redis.GetSubscriber();
    }
}
