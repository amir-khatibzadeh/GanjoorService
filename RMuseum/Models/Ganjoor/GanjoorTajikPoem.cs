﻿using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RMuseum.Models.Ganjoor
{
    public class GanjoorTajikPoem
    {
        /// <summary>
        /// id
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        /// <summary>
        /// cat id
        /// </summary>
        public int CatId { get; set; }

        /// <summary>
        /// title
        /// </summary>
        public string TajikTitle { get; set; }

        /// <summary>
        /// verses text
        /// </summary>
        public string TajikPlainText { get; set; }

        /// <summary>
        /// verses text as html (ganjoor.net format)
        /// </summary>
        public string TajikHtmlText { get; set; }

    }
}