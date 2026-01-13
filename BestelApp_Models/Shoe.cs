using System.ComponentModel.DataAnnotations;

namespace BestelApp_Models
{
    public class Shoe
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Brand { get; set; } = string.Empty;

        [Range(0.01, 10000)]
        public decimal Price { get; set; }

        [Range(20, 50)]
        public int Size { get; set; }

        [MaxLength(100)]
        public string Color { get; set; } = string.Empty;

        
        public override string ToString()
        {
            return $"{Brand} {Name} (Size {Size}) - €{Price}";
        }

        //Dummydata seeding
        public static List<Shoe> SeedingData()
        {
            return new List<Shoe>
            {
                new Shoe
                {
                    Name = "Air Max",
                    Brand = "Nike",
                    Price = 129.99m,
                    Size = 42,
                    Color = "White/Red"
                },

                new Shoe
                {
                    Name = "Classic Leather",
                    Brand = "Reebok",
                    Price = 89.99m,
                    Size = 44,
                    Color = "Black"
                },

                new Shoe
                {
                    Name = "Stan Smith",
                    Brand = "Adidas",
                    Price = 99.99m,
                    Size = 41,
                    Color = "Green/White"

                },

                new Shoe
                {
                    Name = "Yeezy",
                    Brand = "Adidas",
                    Price = 199.99m,
                    Size = 42,
                    Color = "Black/White"
                },

                new Shoe
                {
                    Name = "Random",
                    Brand = "Random",
                    Price = 1.88m,
                    Size = 45,
                    Color = "Black/Yellow"
                },
            };
        }
    }
}
