namespace StockService.Domain
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }

        public bool HasStock(int quantity)
        {
            if (Quantity >= quantity)
            {
                return true;
            }

            return false;
        }

        public void Reserve(int quantity)
        {
            if (!HasStock(quantity))
            {
                throw new InvalidOperationException($"Stok yetersiz: {Name}");
            }
            Quantity -= quantity;
        }

        public void Release(int quantity) 
        { 
            Quantity += quantity; 
        }
    }
}
