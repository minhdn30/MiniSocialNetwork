using Microsoft.AspNetCore.Http;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.Helpers.FileTypeHelpers
{
    public class FileTypeDetector : IFileTypeDetector
    {
        private static readonly HashSet<string> DocumentMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/rtf",
            "application/zip",
            "application/x-zip-compressed",
            "application/x-rar-compressed",
            "application/vnd.rar",
            "application/x-7z-compressed",
            "text/plain",
            "text/csv"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".csv", ".zip", ".rar", ".7z", ".rtf"
        };

        public async Task<MediaTypeEnum?> GetMediaTypeAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // check MIME type 
            MediaTypeEnum? mimeTypeResult = null;
            var mimeType = (file.ContentType ?? string.Empty).ToLowerInvariant();
            var fileName = file.FileName ?? string.Empty;

            if (mimeType.StartsWith("image/")) mimeTypeResult = MediaTypeEnum.Image;
            else if (mimeType.StartsWith("video/")) mimeTypeResult = MediaTypeEnum.Video;
            // other common document types
            else if (mimeType.StartsWith("audio/")) mimeTypeResult = MediaTypeEnum.Audio;
            else if (IsDocumentMimeType(mimeType) || IsDocumentExtension(fileName)) mimeTypeResult = MediaTypeEnum.Document;

            // check Magic Number
            // At least 16 bytes are needed to cover MP4, DOCX, and other complex formats
            byte[] header = new byte[16];
            await using var stream = file.OpenReadStream();

            // Make sure enough bytes can be read.
            if (await stream.ReadAsync(header, 0, header.Length) != header.Length)
            {
                // File too small, signature cannot be determined
                return mimeTypeResult;
            }

            MediaTypeEnum? magicResult = null;

            // --- Image Signatures ---
            // PNG
            if (header.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) magicResult = MediaTypeEnum.Image;
            // JPG/JPEG
            else if (header.Take(3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF })) magicResult = MediaTypeEnum.Image;
            // GIF
            else if (header.Take(6).SequenceEqual(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }) || // GIF89a
                     header.Take(6).SequenceEqual(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 })) magicResult = MediaTypeEnum.Image; // GIF87a

            // --- Video Signatures ---
            // MP4 (Check the 'ftyp' box in bytes 4-7)
            else if (header.Skip(4).Take(4).SequenceEqual(new byte[] { 0x66, 0x74, 0x79, 0x70 })) magicResult = MediaTypeEnum.Video;

            // --- Audio Signatures ---
            // MP3 (ID3v2)
            else if (header.Take(3).SequenceEqual(new byte[] { 0x49, 0x44, 0x33 })) magicResult = MediaTypeEnum.Audio;

            // --- Document Signatures ---
            // PDF
            else if (header.Take(4).SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 })) magicResult = MediaTypeEnum.Document;
            // DOCX/XLSX/PPTX (Office Open XML - start with ZIP)
            else if (header.Take(4).SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) magicResult = MediaTypeEnum.Document;
            // RAR
            else if (header.Take(6).SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 })) magicResult = MediaTypeEnum.Document;
            // 7Z
            else if (header.Take(6).SequenceEqual(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C })) magicResult = MediaTypeEnum.Document;

            // Compare MIME result and Magic Number result
            if (magicResult != null && mimeTypeResult != null)
            {
                if (magicResult == mimeTypeResult)
                    return magicResult; // Same (Verified)
                else
                    return null; // Different (Suspected forgery)
            }

            // If only one of the two is known: prefer Magic Number (more reliable)
            // If both are null, return null.
            return magicResult ?? mimeTypeResult;
        }

        private static bool IsDocumentMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType)) return false;
            if (mimeType.Contains("pdf") || mimeType.Contains("document")) return true;
            if (mimeType.StartsWith("text/")) return true;
            return DocumentMimeTypes.Contains(mimeType);
        }

        private static bool IsDocumentExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext)) return false;
            return DocumentExtensions.Contains(ext);
        }
    }
}
