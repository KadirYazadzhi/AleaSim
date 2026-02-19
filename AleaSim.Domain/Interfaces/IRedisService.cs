using StackExchange.Redis;

namespace AleaSim.Domain.Interfaces;

public interface IRedisService {
    IDatabase GetDatabase();
    ISubscriber GetSubscriber();
}
