namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a single navigational step within a breadcrumb trail.
    /// Used to dynamically generate the navigation path at the top of pages.
    /// </summary>
    public class BreadcrumbItem
    {
        // The display label for the navigation link (e.g., "Home" or "Catalog")
        public string Text { get; set; } = string.Empty;

        // The target destination URL. Defaults to "#" to prevent broken links 
        // if no specific URL is provided during instantiation.
        public string Url { get; set; } = "#";

        // Indicates if this is the current page the user is viewing. 
        // Typically used to disable the link and apply bold styling in the UI.
        public bool IsActive { get; set; } = false;
    }
}