using BenchmarkDotNet;
using BenchmarkDotNet.Disassemblers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Optimize
{

    /// <summary>
    /// Класс для поиска по уникальному ключу
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
 
    public class FastLookup<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _storage = new();

        public void Add(TKey key, TValue value) => _storage[key] = value;

        // Мгновенный поиск
        public TValue? Find(TKey key)
        {
            return _storage.TryGetValue(key, out var value) ? value : default;
        }
    }

    /// <summary>
    /// Бинарный поиск по отсортированным данным 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    
    public class SortedSearcher<T> where T : IComparable<T>
    {
        private readonly List<T> _sortedItems = new();

        public void Add(T item)
        {
            _sortedItems.Add(item);
            _sortedItems.Sort(); // Важно: данные должны быть отсортированы
        }

        public int FindIndex(T target)
        {
            // Сложность O(log n)
            return _sortedItems.BinarySearch(target);
        }
    }

    /// <summary>
    /// Кэширование + Timt to live
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    
    public class SmartCacheWithTtl<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly TimeSpan _ttl;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map;
        private readonly LinkedList<CacheItem> _list;

        public SmartCacheWithTtl(int capacity, TimeSpan ttl)
        {
            _capacity = capacity;
            _ttl = ttl;
            _map = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _list = new LinkedList<CacheItem>();
        }

        public TValue? Get(TKey key)
        {
            if (!_map.TryGetValue(key, out var node))
                return default;

            // ПРОВЕРКА TTL: Если данные устарели — удаляем их
            if (DateTime.UtcNow - node.Value.CreatedAt > _ttl)
            {
                RemoveNode(node);
                return default;
            }

            // Обновляем позицию (LRU логика)
            _list.Remove(node);
            _list.AddFirst(node);

            return node.Value.Value;
        }

        public void Put(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                node.Value.Value = value;
                node.Value.CreatedAt = DateTime.UtcNow; // Обновляем таймер при перезаписи
                _list.Remove(node);
                _list.AddFirst(node);
            }
            else
            {
                if (_map.Count >= _capacity)
                {
                    RemoveNode(_list.Last!);
                }

                var newItem = new CacheItem(key, value, DateTime.UtcNow);
                var newNode = new LinkedListNode<CacheItem>(newItem);
                _list.AddFirst(newNode);
                _map[key] = newNode;
            }
        }

        private void RemoveNode(LinkedListNode<CacheItem> node)
        {
            _map.Remove(node.Value.Key);
            _list.Remove(node);
        }

        private class CacheItem
        {
            public TKey Key { get; }
            public TValue Value { get; set; }
            public DateTime CreatedAt { get; set; }
            public CacheItem(TKey key, TValue value, DateTime createdAt)
                => (Key, Value, CreatedAt) = (key, value, createdAt);
        }
    }

    /// <summary>
    /// Держит классы автоматически отсортированными
    /// </summary>
    /// <typeparam name="T"></typeparam>

    public class AutoSortedList<T> where T : IComparable<T>
    {
        private readonly List<T> _items = new();

        // Свойство для доступа на чтение
        public IReadOnlyList<T> Items => _items;

        public void Add(T item)
        {
            // 1. Ищем место, где должен стоять элемент, через бинарный поиск
            int index = _items.BinarySearch(item);

            // 2. Если BinarySearch возвращает отрицательное число, значит элемента нет.
            // Битовое дополнение (~) этого числа — это индекс, куда его нужно вставить.
            if (index < 0) index = ~index;

            // 3. Вставляем в нужную позицию. Список "раздвигается" сам.
            _items.Insert(index, item);
        }

        public bool Contains(T item)
        {
            // Теперь поиск ВСЕГДА очень быстрый (O(log n))
            return _items.BinarySearch(item) >= 0;
        }

        public int IndexOf(T item)
        {
            int index = _items.BinarySearch(item);
            return index >= 0 ? index : -1;
        }
    }

    /// <summary>
    /// Bloom Filter
    /// </summary>
    /// <typeparam name="T"></typeparam>

    public class BloomFilter<T>
    {
        private readonly BitArray _hashTable;
        private readonly int _hashFunctionsCount;
        private readonly int _size;

        /// <param name="size">Размер битового массива (чем больше, тем меньше ложных срабатываний)</param>
        /// <param name="hashFunctionsCount">Количество хэш-функций (обычно от 3 до 8)</param>
        public BloomFilter(int size, int hashFunctionsCount)
        {
            _size = size;
            _hashFunctionsCount = hashFunctionsCount;
            _hashTable = new BitArray(size);
        }

        // Добавление элемента
        public void Add(T item)
        {
            var primaryHash = item?.GetHashCode() ?? 0;

            for (int i = 0; i < _hashFunctionsCount; i++)
            {
                int hash = ComputeHash(primaryHash, i);
                _hashTable.Set(hash, true);
            }
        }

        // Проверка наличия
        public bool MightContain(T item)
        {
            var primaryHash = item?.GetHashCode() ?? 0;

            for (int i = 0; i < _hashFunctionsCount; i++)
            {
                int hash = ComputeHash(primaryHash, i);
                if (!_hashTable.Get(hash))
                    return false; // Точно нет!
            }

            return true; // Возможно да (есть вероятность ложного срабатывания)
        }

        // Простая имитация нескольких хэш-функций через "подсаливание"
        private int ComputeHash(int primaryHash, int iteration)
        {
            // Используем комбинацию основного хэша и номера итерации
            uint hash = (uint)(primaryHash ^ (iteration * 0x5bd1e995));
            return (int)(hash % (uint)_size);
        }
    }

    /// <summary>
    /// FastStorage — «грубую силу» кэширования с «интеллектом» вероятностного фильтра.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    //Архитектура FastStorage
    //Логика работы будет следующей:
    //Запрос: Приходит ключ.
    //Bloom Filter: Если фильтр говорит «Точно нет» — мгновенно выходим.
    //Smart Cache: Если фильтр сказал «Возможно», проверяем кэш.    
    //Source of Truth: Если в кэше пусто, идем в «медленный» источник (БД), а затем обновляем и кэш, и фильтр.
    public class FastStorage<TKey, TValue> where TKey : notnull
    {
        private readonly SmartCacheWithTtl<TKey, TValue> _cache;
        private readonly BloomFilter<TKey> _filter;
        private readonly Func<TKey, TValue?> _dataSource;

        public FastStorage(
            int capacity,
            TimeSpan ttl,
            int filterSize,
            Func<TKey, TValue?> dataSource)
        {
            _cache = new SmartCacheWithTtl<TKey, TValue>(capacity, ttl);
            _filter = new BloomFilter<TKey>(filterSize, 5); // 5 хэш-функций
            _dataSource = dataSource;
        }

        public TValue? Get(TKey key)
        {
            // Шаг 1: Проверка через Bloom Filter (O(1))
            // Если он говорит "нет", значит в системе этого ключа точно не было
            if (!_filter.MightContain(key))
            {
                return default;
            }

            // Шаг 2: Проверка в быстром кэше
            var cachedValue = _cache.Get(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            // Шаг 3: Если в кэше нет (но фильтр пропустил), идем в источник данных
            var realValue = _dataSource(key);

            if (realValue != null)
            {
                _cache.Put(key, realValue);
                // На всякий случай обновляем фильтр (хотя обычно это делается при добавлении)
                _filter.Add(key);
            }

            return realValue;
        }

        public void Add(TKey key, TValue value)
        {
            _filter.Add(key);
            _cache.Put(key, value);
        }
    }

}
