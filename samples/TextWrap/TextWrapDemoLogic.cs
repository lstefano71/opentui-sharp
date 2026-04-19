using OpenTui.Core;

public readonly record struct TextWrapResizeRect(int Left, int Top, int Width, int Height);

public static class TextWrapDemoLogic
{
    public const int InitialTextBoxInset = 1;
    private const int ContainerBorderThickness = 1;

    public static TextWrapResizeRect ComputeResizedBounds(
        string resizeDirection,
        int deltaX,
        int deltaY,
        int startLeft,
        int startTop,
        int startWidth,
        int startHeight,
        int minWidth,
        int minHeight,
        int containerWidth,
        int containerHeight)
    {
        int left = startLeft;
        int top = startTop;
        int right = startLeft + startWidth;
        int bottom = startTop + startHeight;

        bool adjustsLeft = false;
        bool adjustsRight = false;
        bool adjustsTop = false;
        bool adjustsBottom = false;

        switch (resizeDirection)
        {
            case "nw":
                adjustsLeft = true;
                adjustsTop = true;
                left += deltaX;
                top += deltaY;
                break;
            case "ne":
                adjustsRight = true;
                adjustsTop = true;
                right += deltaX;
                top += deltaY;
                break;
            case "sw":
                adjustsLeft = true;
                adjustsBottom = true;
                left += deltaX;
                bottom += deltaY;
                break;
            case "se":
                adjustsRight = true;
                adjustsBottom = true;
                right += deltaX;
                bottom += deltaY;
                break;
            case "n":
                adjustsTop = true;
                top += deltaY;
                break;
            case "s":
                adjustsBottom = true;
                bottom += deltaY;
                break;
            case "w":
                adjustsLeft = true;
                left += deltaX;
                break;
            case "e":
                adjustsRight = true;
                right += deltaX;
                break;
        }

        // Absolute children are positioned inside the parent's padding box, so the
        // resizable area only excludes the container border.
        int availableWidth = Math.Max(minWidth, containerWidth - (2 * ContainerBorderThickness));
        int availableHeight = Math.Max(minHeight, containerHeight - (2 * ContainerBorderThickness));

        if (adjustsLeft)
        {
            right = Math.Clamp(right, minWidth, availableWidth);
            left = Math.Clamp(left, 0, right - minWidth);
        }
        else if (adjustsRight)
        {
            left = Math.Clamp(left, 0, Math.Max(0, availableWidth - minWidth));
            right = Math.Clamp(right, left + minWidth, availableWidth);
        }
        else
        {
            int width = Math.Clamp(right - left, minWidth, availableWidth);
            left = Math.Clamp(left, 0, availableWidth - width);
            right = left + width;
        }

        if (adjustsTop)
        {
            bottom = Math.Clamp(bottom, minHeight, availableHeight);
            top = Math.Clamp(top, 0, bottom - minHeight);
        }
        else if (adjustsBottom)
        {
            top = Math.Clamp(top, 0, Math.Max(0, availableHeight - minHeight));
            bottom = Math.Clamp(bottom, top + minHeight, availableHeight);
        }
        else
        {
            int height = Math.Clamp(bottom - top, minHeight, availableHeight);
            top = Math.Clamp(top, 0, availableHeight - height);
            bottom = top + height;
        }

        return new TextWrapResizeRect(left, top, right - left, bottom - top);
    }
}
