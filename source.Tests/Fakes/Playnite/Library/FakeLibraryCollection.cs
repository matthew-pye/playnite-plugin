using NSubstitute;
using Playnite;

using System.Collections;

namespace Graviton.Tests.Fakes.Playnite.Library;

public sealed class FakeLibraryCollection<T> where T : LibraryObject
{
    private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);

    public ILibraryCollection<T> Api { get; } = Substitute.For<ILibraryCollection<T>>();

    public FakeLibraryCollection()
    {
        ConfigureQueries();
        ConfigureAdditions();
        ConfigureUpdates();
        ConfigureRemovals();
        ConfigureBulkChanges();
    }

    public void Seed(params T[] items)
    {
        Seed((IEnumerable<T>)items);
    }

    public void Seed(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            AddItem(item);
        }
    }

    #region Setup

    private void ConfigureQueries()
    {
        Api.Count.Returns(_ =>
        {
            return _items.Count;
        });

        Api.Get(id: Arg.Any<string>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);

            if (!_items.TryGetValue(id, out var item))
            {
                return null;
            }

            return Copy(item);
        });

        Api.Get(ids: Arg.Any<IEnumerable<string>>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var items = new List<T>();

            foreach (var id in ids)
            {
                if (_items.TryGetValue(id, out var item))
                {
                    items.Add(Copy(item));
                }
            }

            return items;
        });

        Api.Contains(id: Arg.Any<string>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);
            return _items.ContainsKey(id);
        });

        Api.Contains(item: Arg.Any<T>()).Returns(call =>
        {
            var item = call.ArgAt<T>(0);
            return _items.ContainsKey(item.Id);
        });

        Api.GetEnumerator().Returns(_ =>
        {
            var items = _items.Values.Select(Copy).ToList();
            return items.GetEnumerator();
        });

        ((IEnumerable)Api).GetEnumerator().Returns(_ =>
        {
            var items = _items.Values.Select(Copy).ToList();
            return items.GetEnumerator();
        });
    }

    private void ConfigureAdditions()
    {
        Api.AddAsync(item: Arg.Any<T>()).Returns(call =>
        {
            var item = call.ArgAt<T>(0);
            AddItem(item);
            return Task.CompletedTask;
        });

        Api.AddAsync(item: Arg.Any<T>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var item = call.ArgAt<T>(0);
            AddItem(item);
            return Task.CompletedTask;
        });

        Api.AddAsync(toAdd: Arg.Any<IEnumerable<T>>()).Returns(call =>
        {
            var items = call.ArgAt<IEnumerable<T>>(0);
            Seed(items);
            return Task.CompletedTask;
        });

        Api.AddAsync(toAdd: Arg.Any<IEnumerable<T>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var items = call.ArgAt<IEnumerable<T>>(0);
            Seed(items);
            return Task.CompletedTask;
        });
    }

    private void ConfigureUpdates()
    {
        Api.UpdateAsync(item: Arg.Any<T>()).Returns(call =>
        {
            var item = call.ArgAt<T>(0);
            var result = UpdateItem(item);
            return Task.FromResult(result);
        });

        Api.UpdateAsync(item: Arg.Any<T>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var item = call.ArgAt<T>(0);
            var result = UpdateItem(item);
            return Task.FromResult(result);
        });

        Api.UpdateAsync(items: Arg.Any<IEnumerable<T>>()).Returns(call =>
        {
            var items = call.ArgAt<IEnumerable<T>>(0);
            var results = UpdateItems(items);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(items: Arg.Any<IEnumerable<T>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var items = call.ArgAt<IEnumerable<T>>(0);
            var results = UpdateItems(items);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(id: Arg.Any<string>(), updateAction: Arg.Any<Action<T>>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);
            var updateAction = call.ArgAt<Action<T>>(1);
            var result = UpdateItem(id, updateAction);
            return Task.FromResult(result);
        });

        Api.UpdateAsync(id: Arg.Any<string>(), updateAction: Arg.Any<Action<T>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);
            var updateAction = call.ArgAt<Action<T>>(1);
            var result = UpdateItem(id, updateAction);
            return Task.FromResult(result);
        });

        Api.UpdateAsync(updateAction: Arg.Any<Func<T, bool>>()).Returns(call =>
        {
            var ids = _items.Keys.ToList();
            var updateAction = call.ArgAt<Func<T, bool>>(0);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(updateAction: Arg.Any<Func<T, bool>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var ids = _items.Keys.ToList();
            var updateAction = call.ArgAt<Func<T, bool>>(0);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(ids: Arg.Any<IEnumerable<string>>(), updateAction: Arg.Any<Func<T, bool>>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var updateAction = call.ArgAt<Func<T, bool>>(1);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(ids: Arg.Any<IEnumerable<string>>(), updateAction: Arg.Any<Func<T, bool>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var updateAction = call.ArgAt<Func<T, bool>>(1);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(ids: Arg.Any<IEnumerable<string>>(), updateAction: Arg.Any<Action<T>>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var updateAction = call.ArgAt<Action<T>>(1);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });

        Api.UpdateAsync(ids: Arg.Any<IEnumerable<string>>(), updateAction: Arg.Any<Action<T>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var updateAction = call.ArgAt<Action<T>>(1);
            var results = UpdateItems(ids, updateAction);
            return Task.FromResult(results);
        });
    }

    private void ConfigureRemovals()
    {
        Api.RemoveAsync(id: Arg.Any<string>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);
            var result = RemoveItem(id);
            return Task.FromResult(result);
        });

        Api.RemoveAsync(id: Arg.Any<string>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var id = call.ArgAt<string>(0);
            var result = RemoveItem(id);
            return Task.FromResult(result);
        });

        Api.RemoveAsync(ids: Arg.Any<IEnumerable<string>>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var results = RemoveItems(ids);
            return Task.FromResult(results);
        });

        Api.RemoveAsync(ids: Arg.Any<IEnumerable<string>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var ids = call.ArgAt<IEnumerable<string>>(0);
            var results = RemoveItems(ids);
            return Task.FromResult(results);
        });
    }

    private void ConfigureBulkChanges()
    {
        Api.MakeBulkChangesAsync(toAdd: Arg.Any<IEnumerable<T>>(), toUpdate: Arg.Any<IEnumerable<T>>(), toRemove: Arg.Any<IEnumerable<T>>()).Returns(call =>
        {
            var toAdd = call.ArgAt<IEnumerable<T>>(0);
            var toUpdate = call.ArgAt<IEnumerable<T>>(1);
            var toRemove = call.ArgAt<IEnumerable<T>>(2);

            ApplyBulkChanges(toAdd, toUpdate, toRemove);
            return Task.CompletedTask;
        });

        Api.MakeBulkChangesAsync(toAdd: Arg.Any<IEnumerable<T>>(), toUpdate: Arg.Any<IEnumerable<T>>(),
                                 toRemove: Arg.Any<IEnumerable<T>>(), causation: Arg.Any<string>()).Returns(call =>
        {
            var toAdd = call.ArgAt<IEnumerable<T>>(0);
            var toUpdate = call.ArgAt<IEnumerable<T>>(1);
            var toRemove = call.ArgAt<IEnumerable<T>>(2);

            ApplyBulkChanges(toAdd, toUpdate, toRemove);
            return Task.CompletedTask;
        });
    }

    #endregion

    private void ApplyBulkChanges(IEnumerable<T>? toAdd, IEnumerable<T>? toUpdate, IEnumerable<T>? toRemove)
    {
        if (toAdd is not null)
        {
            Seed(toAdd);
        }

        if (toUpdate is not null)
        {
            UpdateItems(toUpdate);
        }

        if (toRemove is not null)
        {
            foreach (var item in toRemove.ToList())
            {
                RemoveItem(item.Id);
            }
        }
    }

    private void AddItem(T item)
    {
        _items.Add(item.Id, Copy(item));
    }

    private CollectionItemUpdateData<T> UpdateItem(T item)
    {
        if (!_items.TryGetValue(item.Id, out var previous))
        {
            throw new KeyNotFoundException($"No Playnite library item with ID '{item.Id}' exists.");
        }

        var updated = Copy(item);
        _items[item.Id] = updated;

        return new CollectionItemUpdateData<T>(Copy(previous), updated);
    }

    private List<CollectionItemUpdateData<T>> UpdateItems(IEnumerable<T> items)
    {
        return items.Select(UpdateItem).ToList();
    }

    private CollectionItemUpdateData<T> UpdateItem(string id, Action<T> updateAction)
    {
        if (!_items.TryGetValue(id, out var item))
        {
            throw new KeyNotFoundException($"No Playnite library item with ID '{id}' exists.");
        }

        var updated = Copy(item);
        updateAction(updated);
        return UpdateItem(updated);
    }

    private List<CollectionItemUpdateData<T>> UpdateItems(IEnumerable<string> ids, Func<T, bool> updateAction)
    {
        var updates = new List<CollectionItemUpdateData<T>>();

        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (!_items.TryGetValue(id, out var item))
            {
                continue;
            }

            var updated = Copy(item);

            if (updateAction(updated))
            {
                updates.Add(UpdateItem(updated));
            }
        }

        return updates;
    }

    private List<CollectionItemUpdateData<T>> UpdateItems(IEnumerable<string> ids, Action<T> updateAction)
    {
        return UpdateItems(ids, item =>
        {
            updateAction(item);
            return true;
        });
    }

    private List<T> RemoveItems(IEnumerable<string> ids)
    {
        var removed = new List<T>();

        foreach (var id in ids)
        {
            if (RemoveItem(id) is { } item)
            {
                removed.Add(item);
            }
        }

        return removed;
    }

    private T? RemoveItem(string id)
    {
        if (!_items.Remove(id, out var item))
        {
            return null;
        }

        return Copy(item);
    }

    private static T Copy(T item)
    {
        if (item is ICopyable<T> copyable)
        {
            return copyable.GetCopy();
        }

        return item;
    }
}
