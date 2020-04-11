﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsIn.Models
{
    public class Location
    {
        [Required]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; }

        // e.g supermarket, café, shop
        public string Type { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public IEnumerable<Product> Products { get; set; }
    }
}
