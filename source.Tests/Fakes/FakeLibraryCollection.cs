using System.Collections;

using Playnite;

namespace Graviton.Tests.Fakes
{
    public class FakeLibraryCollection<T> : ILibraryCollection<T> where T : LibraryObject
    {
        protected readonly Dictionary<string, T> Items = new();

        public int Count => Items.Count;

        public T? Get(string id) => Items.TryGetValue(id, out var item) ? item : null;

        public List<T> Get(IEnumerable<string> ids) =>
            ids.Select(id => Items.TryGetValue(id, out var item) ? item : null)
               .Where(item => item != null)
               .Select(item => item!)
               .ToList();

        public IEnumerable<T> Query(string query, params object[]? args) =>
            throw new NotSupportedException($"FakeLibraryCollection<{typeof(T).Name}>.Query isn't implemented");

        public Task AddAsync(T item)
        {
            Items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task AddAsync(T item, string causation) => AddAsync(item);

        public Task AddAsync(IEnumerable<T> toAdd)
        {
            foreach (var item in toAdd)
                Items[item.Id] = item;

            return Task.CompletedTask;
        }

        public Task AddAsync(IEnumerable<T> toAdd, string causation) => AddAsync(toAdd);

        public Task<CollectionItemUpdateData<T>> UpdateAsync(T item)
        {
            Items[item.Id] = item;
            return Task.FromResult(new CollectionItemUpdateData<T>(item, item));
        }

        public Task<CollectionItemUpdateData<T>> UpdateAsync(T item, string causation) => UpdateAsync(item);

        public Task<CollectionItemUpdateData<T>> UpdateAsync(string id, Action<T> updateAction)
        {
            if (!Items.TryGetValue(id, out var item))
                throw new KeyNotFoundException($"FakeLibraryCollection<{typeof(T).Name}> has no item with id '{id}'.");

            updateAction(item);
            return Task.FromResult(new CollectionItemUpdateData<T>(item, item));
        }

        public Task<CollectionItemUpdateData<T>> UpdateAsync(string id, Action<T> updateAction, string causation) =>
            UpdateAsync(id, updateAction);

        public async Task<List<CollectionItemUpdateData<T>>> UpdateAsync(IEnumerable<T> items)
        {
            var results = new List<CollectionItemUpdateData<T>>();
            foreach (var item in items)
                results.Add(await UpdateAsync(item));

            return results;
        }

        public Task<List<CollectionItemUpdateData<T>>> UpdateAsync(IEnumerable<T> items, string causation) =>
            UpdateAsync(items);

        public Task<List<CollectionItemUpdateData<T>>> UpdateAsync(Func<T, bool> updateAction)
        {
            var results = Items.Values
                .Where(updateAction)
                .Select(item => new CollectionItemUpdateData<T>(item, item))
                .ToList();

            return Task.FromResult(results);
        }

        public Task<List<CollectionItemUpdateData<T>>> UpdateAsync(Func<T, bool> updateAction, string causation) =>
            UpdateAsync(updateAction);

        public async Task<List<CollectionItemUpdateData<T>>> UpdateAsync(IEnumerable<string> ids, Func<T, bool> updateAction)
        {
            var results = new List<CollectionItemUpdateData<T>>();
            foreach (var item in Get(ids).Where(updateAction))
                results.Add(await UpdateAsync(item));

            return results;
        }

        public Task<List<CollectionItemUpdateData<T>>> UpdateAsync(
            IEnumerable<string> ids, Func<T, bool> updateAction, string causation) =>
            UpdateAsync(ids, updateAction);

        public async Task<List<CollectionItemUpdateData<T>>> UpdateAsync(IEnumerable<string> ids, Action<T> updateAction)
        {
            var results = new List<CollectionItemUpdateData<T>>();
            foreach (var item in Get(ids))
            {
                updateAction(item);
                results.Add(await UpdateAsync(item));
            }

            return results;
        }

        public Task<List<CollectionItemUpdateData<T>>> UpdateAsync(
            IEnumerable<string> ids, Action<T> updateAction, string causation) =>
            UpdateAsync(ids, updateAction);

        public Task<T?> RemoveAsync(string id)
        {
            Items.Remove(id, out var removed);
            return Task.FromResult(removed);
        }

        public Task<T?> RemoveAsync(string id, string causation) => RemoveAsync(id);

        public Task<List<T>> RemoveAsync(IEnumerable<string> ids)
        {
            var removed = new List<T>();
            foreach (var id in ids)
            {
                if (Items.Remove(id, out var item))
                    removed.Add(item);
            }

            return Task.FromResult(removed);
        }

        public Task<List<T>> RemoveAsync(IEnumerable<string> ids, string causation) => RemoveAsync(ids);

        public Task MakeBulkChangesAsync(IEnumerable<T>? toAdd, IEnumerable<T>? toUpdate, IEnumerable<T>? toRemove)
        {
            foreach (var item in toAdd ?? [])
                Items[item.Id] = item;

            foreach (var item in toUpdate ?? [])
                Items[item.Id] = item;

            foreach (var item in toRemove ?? [])
                Items.Remove(item.Id);

            return Task.CompletedTask;
        }

        public Task MakeBulkChangesAsync(
            IEnumerable<T>? toAdd, IEnumerable<T>? toUpdate, IEnumerable<T>? toRemove, string causation) =>
            MakeBulkChangesAsync(toAdd, toUpdate, toRemove);

        public bool Contains(T item) => Items.ContainsKey(item.Id);

        public bool Contains(string id) => Items.ContainsKey(id);

        public IEnumerator<T> GetEnumerator() => Items.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}