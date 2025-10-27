using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;



// ===== Рівень Б: Інтерфейс «Обчислення» + «Калькулятор» =====
public interface ICalculation
{
    /// <summary>
    /// Обчислює суму двох чисел.
    /// </summary>
    /// <param name="a">Перше число.</param>
    /// <param name="b">Друге число.</param>
    /// <returns>Сума a і b.</returns>
    double Sum(double a, double b);
}

public sealed class Calculator : ICalculation
{
    public double Sum(double a, double b) => a + b;
}

// ===== Базова ієрархія для демонстрації успадкування =====
[XmlInclude(typeof(Product))]
[XmlInclude(typeof(Service))]
public abstract class ItemBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public override string ToString() => $"{GetType().Name}: {Name} (Id={Id})";
}

// Клас, на якому демонструємо IComparable<T>
public sealed class Product : ItemBase, IComparable<Product>
{
    public decimal Price { get; set; }

    public Product() { }
    public Product(string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    // Сортування за ціною, далі — за назвою
    public int CompareTo(Product? other)
    {
        if (other is null) return 1;
        int byPrice = Price.CompareTo(other.Price);
        return byPrice != 0 ? byPrice : string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public override string ToString() => base.ToString() + $" | Price: {Price}";
}

public sealed class Service : ItemBase
{
    public decimal HourlyRate { get; set; }
    public int Hours { get; set; }
    [XmlIgnore] public decimal Total => HourlyRate * Hours;

    public Service() { }
    public Service(string name, decimal hourlyRate, int hours)
    {
        Name = name;
        HourlyRate = hourlyRate;
        Hours = hours;
    }

    public override string ToString() => base.ToString() + $" | HourlyRate: {HourlyRate}, Hours: {Hours}, Total: {Total}";
}

// ===== Рівень А: Контейнер із приватною колекцією + серіалізація + IEnumerable + IDisposable =====
public sealed class ItemStore : IEnumerable<ItemBase>, IDisposable
{
    private readonly List<ItemBase> _items = new();
    private bool _disposed;
    private readonly StreamWriter? _log; // демонстрація реального ресурсу під IDisposable

    public ItemStore(string? logPath = null)
    {
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            _log = new StreamWriter(File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
            _log.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Сховище створено.");
        }
    }

    public int Count => _items.Count;

    public void Add(ItemBase item)
    {
        ThrowIfDisposed();
        _items.Add(item);
        _log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] ДОДАНО -> {item}");
    }

    public bool Remove(Guid id)
    {
        ThrowIfDisposed();
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx >= 0)
        {
            _log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] ВИДАЛЕНО -> {_items[idx]}");
            _items.RemoveAt(idx);
            return true;
        }
        return false;
    }

    public IEnumerable<T> OfType<T>() where T : ItemBase
    {
        ThrowIfDisposed();
        return _items.OfType<T>();
    }

    // Серіалізація (XML). Зберігаємо різнорідну колекцію
    public void SaveToFile(string path)
    {
        ThrowIfDisposed();
        var serializer = new XmlSerializer(typeof(List<ItemBase>), new[] { typeof(Product), typeof(Service) });
        using var fs = File.Create(path);
        serializer.Serialize(fs, _items);
        _log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] ЗБЕРЕЖЕНО -> {path} ({_items.Count} об’єктів)");
    }

    public void LoadFromFile(string path)
    {
        ThrowIfDisposed();
        if (!File.Exists(path)) throw new FileNotFoundException("Файл не знайдено", path);
        var serializer = new XmlSerializer(typeof(List<ItemBase>), new[] { typeof(Product), typeof(Service) });
        using var fs = File.OpenRead(path);
        var loaded = (List<ItemBase>)serializer.Deserialize(fs)!;
        _items.Clear();
        _items.AddRange(loaded);
        _log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] ЗАВАНТАЖЕНО <- {path} ({_items.Count} об’єктів)");
    }

    // IEnumerable<T> + IEnumerable
    public IEnumerator<ItemBase> GetEnumerator()
    {
        ThrowIfDisposed();
        return _items.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDisposable
    public void Dispose()
    {
        if (_disposed) return;
        _log?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Сховище звільнено (Dispose).");
        _log?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ItemStore));
    }
}

// ===== Демонстрація роботи (Рівень А і Б) =====
public static class Program
{
    public static void Main()
    {
        // 1) Інтерфейс «Обчислення» + Калькулятор
        ICalculation calc = new Calculator();
        Console.WriteLine($"Сума(3.5, 4.2) = {calc.Sum(3.5, 4.2)}");

        // 2) Робота з контейнером: різнорідні об’єкти
        string dataPath = Path.Combine(AppContext.BaseDirectory, "items.xml");
        using (var store = new ItemStore(logPath: Path.Combine(AppContext.BaseDirectory, "store.log")))
        {
            store.Add(new Product("Laptop", 1500m));
            store.Add(new Service("Hosting", 10m, 12));
            store.Add(new Product("Mouse", 25.99m));
            store.Add(new Service("Support", 30m, 2));

            Console.WriteLine("\n-- Усі елементи:");
            foreach (var it in store)
                Console.WriteLine(it);

            // 3) IComparable<T> на Product: сортування за ціною
            Console.WriteLine("\n-- Продукти (відсортовано за ціною):");
            var sortedProducts = store.OfType<Product>().OrderBy(p => p).ToList();
            foreach (var p in sortedProducts)
                Console.WriteLine(p);

            // 4) Серіалізація
            store.SaveToFile(dataPath);
            Console.WriteLine($"\nЗбережено до файлу: {dataPath}");
        }

        // 5) Завантаження з файлу + демонстрація IEnumerable після десеріалізації
        var store2 = new ItemStore();
        store2.LoadFromFile(dataPath);
        Console.WriteLine("\n-- Після завантаження з файлу:");
        foreach (var it in store2)
            Console.WriteLine(it);
        store2.Dispose();
    }
}