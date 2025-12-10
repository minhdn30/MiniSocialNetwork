using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Helpers.FileTypeHelpers
{
    public class FileTypeDetector : IFileTypeDetector
    {
        public async Task<MediaTypeEnum?> GetMediaTypeAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // check MIME type 
            MediaTypeEnum? mimeTypeResult = null;
            var mimeType = file.ContentType.ToLower();

            if (mimeType.StartsWith("image/")) mimeTypeResult = MediaTypeEnum.Image;
            else if (mimeType.StartsWith("video/")) mimeTypeResult = MediaTypeEnum.Video;

            // other common document types
            else if (mimeType.StartsWith("audio/")) mimeTypeResult = MediaTypeEnum.Audio;
            else if (mimeType.Contains("pdf") || mimeType.Contains("document")) mimeTypeResult = MediaTypeEnum.Document;

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
            // DOCX/XLSX/PPTX (Office Open XML - Bắt đầu bằng chữ ký ZIP)
            else if (header.Take(4).SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) magicResult = MediaTypeEnum.Document;

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
    }
}
