using System.Buffers.Binary;

namespace Quantitary.Services;

public static class ChatImageValidator
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public static bool TryValidate(ChatImage? image, out ChatImage? safeImage, out string? error)
    {
        safeImage = null;
        error = null;
        if (image is null) return true;

        if (string.IsNullOrWhiteSpace(image.MediaType) || !AllowedTypes.Contains(image.MediaType))
        {
            error = "Solo podés adjuntar imágenes JPG, PNG o WebP.";
            return false;
        }

        var data = image.Base64Data?.Trim() ?? string.Empty;
        if (data.Length == 0 || data.Length > ((AiChatLimits.MaxImageBytes + 2) / 3) * 4 + 8)
        {
            error = "La imagen supera el límite de 4 MB.";
            return false;
        }

        byte[] bytes;
        try { bytes = Convert.FromBase64String(data); }
        catch (FormatException)
        {
            error = "No se pudo leer la imagen adjunta.";
            return false;
        }

        if (bytes.Length == 0 || bytes.Length > AiChatLimits.MaxImageBytes)
        {
            error = "La imagen supera el límite de 4 MB.";
            return false;
        }

        if (!TryReadDimensions(bytes, image.MediaType, out var width, out var height))
        {
            error = "El archivo no contiene una imagen válida.";
            return false;
        }

        if (width < 1 || height < 1 || width > AiChatLimits.MaxImageEdge || height > AiChatLimits.MaxImageEdge)
        {
            error = $"La imagen debe medir como máximo {AiChatLimits.MaxImageEdge} × {AiChatLimits.MaxImageEdge} píxeles.";
            return false;
        }

        var cleanName = Path.GetFileName(image.FileName ?? "imagen");
        if (cleanName.Length > 120) cleanName = cleanName[..120];
        safeImage = new ChatImage(image.MediaType.ToLowerInvariant(), data, cleanName, width, height);
        return true;
    }

    private static bool TryReadDimensions(byte[] bytes, string mediaType, out int width, out int height)
    {
        width = height = 0;
        return mediaType.ToLowerInvariant() switch
        {
            "image/png" => TryPng(bytes, out width, out height),
            "image/jpeg" => TryJpeg(bytes, out width, out height),
            "image/webp" => TryWebP(bytes, out width, out height),
            _ => false
        };
    }

    private static bool TryPng(byte[] b, out int width, out int height)
    {
        width = height = 0;
        if (b.Length < 24 || b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return false;
        width = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20, 4));
        return width > 0 && height > 0;
    }

    private static bool TryJpeg(byte[] b, out int width, out int height)
    {
        width = height = 0;
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return false;
        var index = 2;
        while (index + 9 < b.Length)
        {
            if (b[index] != 0xFF) { index++; continue; }
            while (index < b.Length && b[index] == 0xFF) index++;
            if (index >= b.Length) return false;
            var marker = b[index++];
            if (marker is 0xD8 or 0xD9) continue;
            if (index + 1 >= b.Length) return false;
            var length = (b[index] << 8) + b[index + 1];
            if (length < 2 || index + length > b.Length) return false;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                height = (b[index + 3] << 8) + b[index + 4];
                width = (b[index + 5] << 8) + b[index + 6];
                return width > 0 && height > 0;
            }
            index += length;
        }
        return false;
    }

    private static bool TryWebP(byte[] b, out int width, out int height)
    {
        width = height = 0;
        if (b.Length < 30 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' ||
            b[8] != 'W' || b[9] != 'E' || b[10] != 'B' || b[11] != 'P') return false;
        var kind = System.Text.Encoding.ASCII.GetString(b, 12, 4);
        if (kind == "VP8X")
        {
            width = 1 + b[24] + (b[25] << 8) + (b[26] << 16);
            height = 1 + b[27] + (b[28] << 8) + (b[29] << 16);
            return true;
        }
        if (kind == "VP8 " && b.Length >= 30)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(28, 2)) & 0x3FFF;
            return width > 0 && height > 0;
        }
        if (kind == "VP8L" && b.Length >= 25 && b[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(21, 4));
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
            return true;
        }
        return false;
    }
}
