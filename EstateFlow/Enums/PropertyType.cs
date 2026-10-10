using System.ComponentModel.DataAnnotations;

namespace EstateFlow.Enums
{
    public enum PropertyType
    {
        [Display(Name = "Апартамент")]
        Apartment,

        [Display(Name = "Къща")]
        House,

        [Display(Name = "Парцел")]
        Land,

        [Display(Name = "Офис")]
        Office,

        [Display(Name = "Гараж")]
        Garage
    }
}
