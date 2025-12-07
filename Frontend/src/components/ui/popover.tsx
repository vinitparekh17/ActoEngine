import * as React from "react";
import * as PopoverPrimitive from "@radix-ui/react-popover";

import { cn } from "@/lib/utils";

/**
 * Wraps the Radix PopoverRoot and forwards all received props.
 *
 * @param props - Props to pass through to `PopoverPrimitive.Root`; `data-slot="popover"` is added to the rendered element.
 * @returns A `PopoverPrimitive.Root` React element with the forwarded props and `data-slot="popover"`.
 */
function Popover({
  ...props
}: React.ComponentProps<typeof PopoverPrimitive.Root>) {
  return <PopoverPrimitive.Root data-slot="popover" {...props} />;
}

/**
 * Wrapper around the Radix PopoverTrigger that attaches a tracking data attribute.
 *
 * @returns The Radix PopoverTrigger element with `data-slot="popover-trigger"` and any provided props
 */
function PopoverTrigger({
  ...props
}: React.ComponentProps<typeof PopoverPrimitive.Trigger>) {
  return <PopoverPrimitive.Trigger data-slot="popover-trigger" {...props} />;
}

/**
 * Renders the popover's content inside a portal with sensible defaults and styling.
 *
 * @param className - Additional CSS class names to merge with the component's default classes
 * @param align - Alignment of the content relative to the trigger ("start", "center", or "end")
 * @param sideOffset - Distance in pixels between the trigger and the content
 * @returns The Popover content element (wrapped in a portal) with `data-slot="popover-content"` and merged classes
 */
function PopoverContent({
  className,
  align = "center",
  sideOffset = 4,
  ...props
}: React.ComponentProps<typeof PopoverPrimitive.Content>) {
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        data-slot="popover-content"
        align={align}
        sideOffset={sideOffset}
        className={cn(
          "bg-popover text-popover-foreground data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 z-50 w-72 origin-(--radix-popover-content-transform-origin) rounded-md border p-4 shadow-md outline-hidden",
          className,
        )}
        {...props}
      />
    </PopoverPrimitive.Portal>
  );
}

/**
 * Renders a Radix Popover.Anchor with forwarded props and a tracking data-slot.
 *
 * @param props - Props forwarded to Radix PopoverPrimitive.Anchor
 * @returns The rendered Popover anchor element
 */
function PopoverAnchor({
  ...props
}: React.ComponentProps<typeof PopoverPrimitive.Anchor>) {
  return <PopoverPrimitive.Anchor data-slot="popover-anchor" {...props} />;
}

export { Popover, PopoverTrigger, PopoverContent, PopoverAnchor };
