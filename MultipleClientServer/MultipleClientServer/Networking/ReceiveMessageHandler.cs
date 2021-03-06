﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ClientServer.Networking {

    internal class ReceiveMessageHandler {

        /// <summary>
        /// Handles prefix processing for incoming messages.
        /// </summary>
        /// <param name="receiveEA">The receive event agrs.</param>
        /// <param name="token">The user token of the event agrs.</param>
        /// <param name="bytesToProcess">The amount of bytes that need to be processed.</param>
        public int HandlePrefix(SocketAsyncEventArgs receiveEA, ReceiveToken token, int bytesToProcess) {
            // create the prefix byte array
            if (token.PrefixBytesReceived == 0) {
                token.Prefix = new byte[Constants.PREFIX_SIZE];
            }

            // prefix has been received completely
            if (bytesToProcess >= Constants.PREFIX_SIZE - token.PrefixBytesReceived) {
                // copy data bytes
                Buffer.BlockCopy(
                    receiveEA.Buffer,
                    token.TextOffset - Constants.PREFIX_SIZE + token.PrefixBytesReceived,
                    token.Prefix,
                    token.PrefixBytesReceived,
                    Constants.PREFIX_SIZE - token.PrefixBytesReceived);

                // process prefix
                token.Type = (MessageType)token.Prefix[0];
                token.TextLength = BitConverter.ToInt32(getSubArray(token.Prefix,
                    Constants.TEXT_LENGTH_PREFIX_OFFSET,
                    Constants.LENGTH_PREFIX_SIZE), 0);
                token.FileLength = BitConverter.ToInt64(getSubArray(token.Prefix,
                    Constants.FILE_LENGTH_PREFIX_OFFSET,
                    Constants.LENGTH_PREFIX_SIZE * 2), 0);

                bytesToProcess = bytesToProcess - Constants.PREFIX_SIZE + token.PrefixBytesReceived;
                token.PrefixBytesProcessed = Constants.PREFIX_SIZE - token.PrefixBytesReceived;
                token.PrefixBytesReceived = Constants.PREFIX_SIZE;

                // append file offset
                token.FileOffset += token.TextLength;
            }
            // prefix has been received incompletely
            else {
                // copy prefix bytes
                Buffer.BlockCopy(
                    receiveEA.Buffer,
                    token.TextOffset - Constants.PREFIX_SIZE + token.PrefixBytesReceived,
                    token.Prefix,
                    token.PrefixBytesReceived,
                    bytesToProcess);

                token.PrefixBytesProcessed = bytesToProcess;
                token.PrefixBytesReceived += bytesToProcess;
                bytesToProcess = 0;
            }

            // set new offset
            if (bytesToProcess == 0) {
                token.TextOffset -= token.PrefixBytesProcessed;
                token.FileOffset -= token.PrefixBytesProcessed;
                token.PrefixBytesProcessed = 0;
            }
            return bytesToProcess;
        }

        /// <summary>
        /// Handles data processing for incoming messages.
        /// </summary>
        /// <param name="receiveEA">The receive event agrs.</param>
        /// <param name="token">The user token of the event agrs.</param>
        /// <param name="bytesToProcess">The amount of bytes that need to be processed.</param>
        public int HandleText(SocketAsyncEventArgs receiveEA, ReceiveToken token, int bytesToProcess) {
            // create the text byte array
            if (token.TextBytesReceived == 0) {
                token.Data = new byte[token.TextLength];
            }

            // text data has been received completely
            if (bytesToProcess >= token.TextLength - token.TextBytesReceived) {
                // copy text data bytes
                Buffer.BlockCopy(
                    receiveEA.Buffer,
                    token.TextOffset,
                    token.Data,
                    token.TextBytesReceived,
                    token.TextLength - token.TextBytesReceived);

                // set byte counters
                bytesToProcess = bytesToProcess - token.TextLength + token.TextBytesReceived;
                token.TextBytesProcessed = token.TextLength - token.TextBytesReceived;
                token.TextBytesReceived = token.TextLength;
                if (token.RespectPrefixRemainderForOffset) {
                    token.FileOffset -= token.PrefixBytesProcessed;
                }
                
                token.FilePath = Encoding.Default.GetString(token.Data);
            }
            // text data has been received incompletely
            else {
                // copy text data bytes
                Buffer.BlockCopy(
                    receiveEA.Buffer,
                    token.TextOffset,
                    token.Data,
                    token.TextBytesReceived,
                    bytesToProcess);

                token.TextBytesProcessed = bytesToProcess;
                token.TextBytesReceived += bytesToProcess;
                bytesToProcess = 0;
            }

            // set new offset
            if (bytesToProcess == 0) {
                token.TextOffset = token.BufferOffset;
                token.FileOffset -= token.TextBytesProcessed;
                token.RespectPrefixRemainderForOffset = true;
                token.TextBytesProcessed = 0;
            }
            return bytesToProcess;
        }

        /// <summary>
        /// Helper function to handle file processing.
        /// </summary>
        /// <param name="receiveEA">The receive event agrs.</param>
        /// <param name="token">The user token of the event agrs.</param>
        /// <param name="bytesToProcess">The amount of bytes that need to be processed.</param>
        internal bool HandleFile(SocketAsyncEventArgs receiveEA, ReceiveToken token, int bytesToProcess) {
            // create writer
            if (token.FileBytesReceived == 0) {
                token.Writer = new BinaryWriter(File.Open(token.FilePath, FileMode.Create));
            }

            // file has been received completely.
            if (bytesToProcess + token.FileBytesReceived == token.FileLength) {
                token.Writer.Write(receiveEA.Buffer, token.FileOffset, bytesToProcess);
                token.Writer.Flush();
                return true;
            }
            // file has been received incompletely
            else {
                token.Writer.Write(receiveEA.Buffer, token.FileOffset, bytesToProcess);
                token.Writer.Flush();
                token.FileBytesReceived += bytesToProcess;
                return false;
            }
        }

        /// <summary>
        /// Helper function to get a sub (byte) array.
        /// </summary>
        /// <param name="array">The source array.</param>
        /// <param name="offset">The source offset.</param>
        /// <param name="count">The amount of bytes for the sub array.</param>
        private byte[] getSubArray(byte[] array, int offset, int count) {
            byte[] subArray = new byte[count];
            Buffer.BlockCopy(array, offset, subArray, 0, count);
            return subArray;
        }
    }
}
