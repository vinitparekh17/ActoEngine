import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';

interface BreadcrumbItem {
  label: string;
  href?: string;
}

/**
 * Render a horizontal breadcrumb navigation from an ordered list of breadcrumb items.
 *
 * Each item's label is displayed; items that include `href` are rendered as links and
 * items without `href` are rendered as plain text. Chevron icons separate consecutive items.
 *
 * @param items - Array of breadcrumb items where each item has a `label` and optional `href`
 * @returns A `nav` element containing the breadcrumb trail with chevron separators
 */
export function DetailBreadcrumbs({ items }: { items: BreadcrumbItem[] }) {
  return (
    <nav className="flex items-center space-x-1 text-sm text-muted-foreground mb-4">
      {items.map((item, index) => (
        <div key={index} className="flex items-center">
          {index > 0 && <ChevronRight className="h-4 w-4 mx-1" />}
          {item.href ? (
            <Link to={item.href} className="hover:text-foreground">
              {item.label}
            </Link>
          ) : (
            <span className="text-foreground font-medium">{item.label}</span>
          )}
        </div>
      ))}
    </nav>
  );
}