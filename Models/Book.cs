using System;

namespace Models
{
    public class Book
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
