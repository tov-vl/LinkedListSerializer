﻿using SerializerTests.Interfaces;
using SerializerTests.Model;
using SerializerTests.Nodes;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System;
using SerializerTests.Mappers;
using SerializerTests.Helpers;
using SerializerTests.Options;

namespace SerializerTests.Implementations
{
    public class ListSerializer : IListSerializer
    {
        private readonly IMapper<ListNode, IDictionary<ListNode, NodeDto>> _listNodeToNodeDtosMapper = new ListNodeToNodeDtosMapper();
        private readonly IMapper<List<NodeDto>, ListNode> _nodeDtosToListNodeMapper = new NodeDtosToListNodeMapper();

        //the constructor with no parameters is required and no other constructors can be used.
        public ListSerializer()
        {
            //...
        }

        public Task<ListNode> DeepCopy(ListNode head)
        {
            var nodeDtoDict = _listNodeToNodeDtosMapper.Map(head);

            var headCopy = _nodeDtosToListNodeMapper.Map(nodeDtoDict.Values.ToList());

            return Task.FromResult(headCopy);
        }

        public async Task Serialize(ListNode head, Stream stream)
        {
            var nodeDtoDict = _listNodeToNodeDtosMapper.Map(head);

            if (nodeDtoDict.Count <= ListSerializerOptions.CustomSerializationThreshold)
                await ListSerializerHelper.Serialize(nodeDtoDict, stream, usePrettyFormatting: true);
            else
                await ListSerializerHelper.SerializeWithJsonSerializer(nodeDtoDict, stream, usePrettyFormatting: true);
        }

        public async Task<ListNode> Deserialize(Stream stream)
        {
            var nodeDtos = await DeserializeAsNodeDtos(stream);

            if (nodeDtos.Count == 0)
                throw new ArgumentException("No data in the stream");

            var head = _nodeDtosToListNodeMapper.Map(nodeDtos);

            return head;
        }

        private static async Task<List<NodeDto>> DeserializeAsNodeDtos(Stream stream)
        {
            var res = new List<NodeDto>();

            var node = new NodeDto();
            var buffer = new byte[1];
            var propertyIndex = 0;
            var depth = 0;
            var propertyValueBuilder = new StringBuilder();
            var propertyNameBuilder = new StringBuilder();

            while (await stream.ReadAsync(buffer.AsMemory(0, 1)) > 0)
            {
                if (depth > 1)
                    throw new JsonException("Wrong nesting depth of the JSON file", (stream as FileStream)?.Name, null, null);

                if (propertyIndex > NodeDto.PropertiesCount)
                    throw new JsonException($"Wrong number of object properties (expected {NodeDto.PropertiesCount} properties", (stream as FileStream)?.Name, null, null);

                var ch = (char)buffer[0];

                if (ch == '{')
                {
                    if (depth == 0)
                        node = new NodeDto();

                    depth++;
                }
                else if (depth > 0 && (ch == ',' || ch == '}'))
                {
                    propertyIndex++;

                    var propertyValue = propertyValueBuilder.ToString();
                    var propertyName = propertyNameBuilder.ToString();
                    switch (propertyName)
                    {
                        case NodeDto.IndexName:
                            node.Index = int.Parse(propertyValue);
                            break;
                        case NodeDto.RandomIndexName:
                            if (propertyValue == "null")
                                node.RandomIndex = null;
                            else
                                node.RandomIndex = int.Parse(propertyValue);
                            break;
                        case NodeDto.DataName:
                            if (propertyValue == "null")
                                node.Data = null;
                            else
                                node.Data = propertyValue;
                            break;
                        default:
                            throw new JsonException($"Wrong name of the property: \"{propertyName}\"", (stream as FileStream)?.Name, null, null);
                    }

                    propertyValueBuilder.Clear();
                    propertyNameBuilder.Clear();

                    if (ch == '}')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            res.Add(node);
                            propertyIndex = 0;
                        }
                    }
                }
                else if (ch == '"')
                {
                    var builder = new StringBuilder();
                    while (await stream.ReadAsync(buffer.AsMemory(0, 1)) > 0)
                    {
                        var nextChar = (char)buffer[0];
                        if (nextChar == '"')
                            break;

                        builder.Append(nextChar);
                    }

                    if (propertyNameBuilder.Length == 0)
                        propertyNameBuilder = builder;
                    else
                        propertyValueBuilder = builder;
                }
                else if (char.IsLetterOrDigit(ch))
                {
                    propertyValueBuilder.Append(ch);
                }
            }

            return res;
        }
    }
}