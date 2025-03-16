using System;
using System.IO;

namespace RemapIdenticalRBRTextures
{
    //-----------------------------------------------------
    // RBZ file details (file path, file size, content for duplicate checks)
    //
    public class RBZFileInfo
    {
        private FileInfo fileInfo;

        private bool headerFingerprintProcessed;    // Header fingerprint already read
        private int headerFingerprintSize;          // Size of the fingerprint in bytes (could be less than sizeof headerFingerprint array if the total filesize is less)

        private byte[] headerFingerprint;           // Content of the file header fingerprint
        public byte[] HeaderFingerprint { get { return headerFingerprint; } }

        private byte[] fileContent;                 // Whole file content if the header was identical and we have to compare the whole file

        public bool Processed { get; set; }         // If file has been processed then no need to re-check it (it was already found to be identical or no identical files found)

        private long fileSize;
        public long FileSize
        {
            get
            {
                if (fileSize < 0) fileSize = fileInfo.Length;
                return fileSize;
            }
        }

        public string FileFullName { get { return fileInfo.FullName; } }

        public void ReleaseContentBuffers()
        {
            // Release byte arrays because this file has been processed and no need to re-check it
            this.headerFingerprint = null;
            this.fileContent = null;
        }

        public int GetHeaderFingerprintSize()
        {
            if (!headerFingerprintProcessed || headerFingerprint == null)
            {
                headerFingerprintProcessed = true;
                headerFingerprint = new byte[4096];
                using (var fs = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    headerFingerprintSize = fs.Read(HeaderFingerprint, 0, HeaderFingerprint.GetUpperBound(0) + 1);
                }
            }

            return headerFingerprintSize;
        }


        // Are files identical? Could use hashKey here, but do the comparing using brute-force byte array comparing (optimized using cached header buffer because often files differ already during the first block)
        public bool IsFileIdentical(RBZFileInfo otherFile)
        {
            if (this.IsHeaderFingerprintIdentical(otherFile))
            {
                // If headers are identical then check rest of the file also to be sure files really are identical.
                // But usually if files were different then it is spotted already in the header checks, so non-identical files very rarely comes to this slower block
                if (this.fileContent == null)
                    this.fileContent = File.ReadAllBytes(fileInfo.FullName);

                if (otherFile.fileContent == null)
                    otherFile.fileContent = File.ReadAllBytes(otherFile.fileInfo.FullName);

                int thisFileSize = this.fileContent.GetUpperBound(0) + 1;
                int otherFileSize = otherFile.fileContent.GetUpperBound(0) + 1;
                if (thisFileSize == otherFileSize)
                {
                    for (int idx = this.GetHeaderFingerprintSize(); idx < thisFileSize; idx++)
                    {
                        if (this.fileContent[idx] != otherFile.fileContent[idx])
                            return false;
                    }

                    return true;
                }
                return false;
            }

            return false;
        }

        // Are headers of two files identical? The content of the header is cached in-memory and readin only once per file to speed up initial comparison
        private bool IsHeaderFingerprintIdentical(RBZFileInfo otherFile)
        {
            int idx;
            int thisHeaderSize = this.GetHeaderFingerprintSize();
            int otherHeaderSize = otherFile.GetHeaderFingerprintSize();

            if (thisHeaderSize == otherHeaderSize)
            {
                if (thisHeaderSize > 128)
                {
                    // Many DDS textures may have identical header in the first 128 block, so start comparing after the initial block if the fileSize>128 bytes
                    for (idx = 128; idx < thisHeaderSize; idx++)
                    {
                        if (this.headerFingerprint[idx] != otherFile.headerFingerprint[idx])
                            return false;
                    }
                }

                // Compare the lower 128 block if >128 block was identical
                for (idx = 0; idx < 128 && idx < thisHeaderSize; idx++)
                {
                    if (this.headerFingerprint[idx] != otherFile.headerFingerprint[idx])
                        return false;
                }

                return true;
            }
            return false;
        }

        public RBZFileInfo(FileInfo fileInfo)
        {
            this.Processed = false;
            this.fileInfo = fileInfo;
            this.fileSize = -1;

            this.fileContent = null;
            this.headerFingerprint = null;
            this.headerFingerprintProcessed = false;
            this.headerFingerprintSize = 0;
        }
    }

}
