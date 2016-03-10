﻿using System.Collections.Generic;
using System.Linq;
using BackendGeneral.Interfaces;
using BackendGeneral.Providers;
using System.Threading.Tasks;

namespace BackendGeneral
{
    public abstract class Service<T>
        where T : RepositoryProvider
    {
        protected readonly T RepositoryProvider;
        private readonly ICache _cache;
        private readonly IDbContext _dbContext;
        private readonly ILogger _logger;

        //TODO: Web.config
        private const string Dependency = "dependency";
        private const string Expression = "expression";
        private const string Item = "item";

        protected Service(T repositoryProvider, T serviceProvider, IDbContext dbContext, ILogger logger, ICache cache)
        {
            RepositoryProvider = repositoryProvider;
            _cache = cache;
            _dbContext = dbContext;
            _logger = logger;
        }

        protected void CacheItem(IIdentifiable item)
        {
            string objectName = item.GetType().Name;
            _cache.Set(string.Format("{0}_{1}_{2}", Item, objectName, item.Id), item);
        }

        protected void RemoveItemFromCache<T6>(T6 item)
            where T6 : IIdentifiable
        {
            string objectName = typeof(T6).Name;
            _cache.Remove(string.Format("{0}_{1}_{2}", Item, objectName, item.Id));
        }

        protected void CacheQuery<T6>(string expression, List<T6> items)
        {
            //Is an type that can be identified by an Id
            if (typeof(T6).GetInterfaces().Contains(typeof(IIdentifiable)))
            {
                List<IIdentifiable> lstIdentifiables = items.Cast<IIdentifiable>().ToList();

                foreach (IIdentifiable item in lstIdentifiables)
                {
                    CacheItem(item);
                }

                _cache.Set(string.Format("{0}_{1}", Expression, expression), lstIdentifiables.Select(item => item.Id).ToList());
            }
            else
            {
                _cache.Set(string.Format("{0}_{1}", Expression, expression), items);
            }
        }

        public async Task<List<T6>> ReadQueryAsync<T6>(IQueryable<T6> query)
        {
            string expression = _dbContext.GetQueryableAsString<T6>(query);
            List<T6> lstCacheItems;

            if (typeof(T6).GetInterfaces().Contains(typeof(IIdentifiable)))
            {

                List<int> lstIds = _cache.Get<List<int>>(string.Format("{0}_{1}", Expression, expression));
                if (lstIds != null)
                {
                    lstCacheItems = new List<T6>();
                    string objectName = typeof(T6).Name;
                    foreach (int id in lstIds)
                    {
                        lstCacheItems.Add(_cache.Get<T6>(string.Format("{0}_{1}_{2}", Item, objectName, id)));
                    }
                }
                else
                {
                    lstCacheItems = await ReadQueryAndCacheAsync(query, expression);
                }
            }
            else
            {
                lstCacheItems = _cache.Get<List<T6>>(string.Format("{0}_{1}", Expression, expression));

                if (lstCacheItems == null)
                {
                    lstCacheItems = await ReadQueryAndCacheAsync(query, expression);
                }
            }

            return lstCacheItems;
        }

        public List<T6> ReadQuery<T6>(IQueryable<T6> query)
        {
            string expression = _dbContext.GetQueryableAsString<T6>(query);
            List<T6> lstCacheItems;

            if (typeof(T6).GetInterfaces().Contains(typeof(IIdentifiable)))
            {

                List<int> lstIds = _cache.Get<List<int>>(string.Format("{0}_{1}", Expression, expression));
                if (lstIds != null)
                {
                    lstCacheItems = new List<T6>();
                    string objectName = typeof(T6).Name;
                    foreach (int id in lstIds)
                    {
                        lstCacheItems.Add(_cache.Get<T6>(string.Format("{0}_{1}_{2}", Item, objectName, id)));
                    }
                }
                else
                {
                    lstCacheItems = ReadQueryAndCache(query, expression);
                }
            }
            else
            {
                lstCacheItems = _cache.Get<List<T6>>(string.Format("{0}_{1}", Expression, expression));

                if (lstCacheItems == null)
                {
                    lstCacheItems = ReadQueryAndCache(query, expression);
                }
            }

            return lstCacheItems;
        }

        private async Task<List<T>> ReadQueryAndCacheAsync<T>(IQueryable<T> query, string expression)
        {
            List<T> lstCacheItems = await _dbContext.ReadQueryAsync(query);

            CacheQuery(expression, lstCacheItems);

            CacheDependencies(query, expression);

            return lstCacheItems;
        }

        private List<T> ReadQueryAndCache<T>(IQueryable<T> query, string expression)
        {
            List<T> lstCacheItems = _dbContext.ReadQuery(query);

            CacheQuery(expression, lstCacheItems);

            CacheDependencies(query, expression);

            return lstCacheItems;
        }

        private void CacheDependencies<T>(IQueryable<T> query, string expression)
        {
            //Dependencies
            List<string> dependencies = _dbContext.GetDependencies(query);
            foreach (string dependency in dependencies)
            {
                CacheDependency(expression, dependency);
            }
        }

        private void CacheDependency(string expression, string dependency)
        {
            List<string> expressions = _cache.Get<List<string>>(string.Format("{0}_{1}", Dependency, dependency));
            expressions = expressions ?? new List<string>();
            expressions.Add(expression);

            _cache.Set(string.Format("{0}_{1}", Dependency, dependency), expressions);
        }

        private void RemoveCacheDependencyExpressions(string dependency)
        {
            List<string> expressions = _cache.Get<List<string>>(string.Format("{0}_{1}", Dependency, dependency));

            if (expressions != null)
            {
                foreach (string expression in expressions)
                {
                    _cache.Remove(string.Format("{0}_{1}", Expression, expression));
                }

                _cache.Set(string.Format("{0}_{1}", Dependency, dependency), new List<string>());
            }
        }
    }
}
