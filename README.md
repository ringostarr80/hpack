# hpack

[![License: CC0](https://img.shields.io/github/license/ringostarr80/hpack.svg)](https://www.apache.org/licenses/LICENSE-2.0)
![NuGet](https://img.shields.io/nuget/v/hpack)
![Version](https://img.shields.io/github/v/tag/ringostarr80/hpack?sort=semver)
![Workflow: CodeQL](https://img.shields.io/github/actions/workflow/status/ringostarr80/hpack/.github/workflows/codeql-analysis.yml?branch=main)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=ringostarr80_hpack&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=ringostarr80_hpack)

Header Compression for HTTP/2 written in C#

### An Example on how to use the hpack.Encoder and hpack.Decoder

First, you have to create a HeaderListener that implements IHeaderListener

```c#
using System;
using System.Text;

namespace HTTP2
{
  public class EmitHeaderEventArgs : EventArgs
  {
    private string _name;
    private string _value;
    
    public string Name { get { return this._name; } }
    
    public string Value { get { return this._value; } }
    
    public EmitHeaderEventArgs(byte[] name, byte[] value)
    {
      this._name = Encoding.UTF8.GetString(name);
      this._value = Encoding.UTF8.GetString(value);
    }
    
    public EmitHeaderEventArgs(string name, string value)
    {
      this._name = name;
      this._value = value;
    }
  }
  
  public class HeaderListener : hpack.IHeaderListener
  {
    public delegate void EmitHeaderEventHandler(object sender, EmitHeaderEventArgs e);
    
    public event EmitHeaderEventHandler HeaderEmitted;
    
    public HeaderListener()
    {
      
    }
    
    protected virtual void OnHeaderEmitted(EmitHeaderEventArgs e)
    {
      if (this.HeaderEmitted != null) {
        this.HeaderEmitted(this, e);
      }
    }
    
    public void AddHeader(byte[] name, byte[] value, bool sensitive)
    {
      this.OnHeaderEmitted(new EmitHeaderEventArgs(name, value));
    }
  }
}
```

Then you can instantiate this HeaderListener and add an EventListener

```c#
using System;

namespace HTTP2
{
  class MainClass
  {
    public static void Main(string[] args)
    {
      var headerListener = new HeaderListener();
      headerListener.HeaderEmitted += (object sender, EmitHeaderEventArgs e) {
        Console.WriteLine("header emitted => name: " + e.Name + "; value: " + e.Value);
      };
    }
  }
}
```

Now you can encode and decode headers

```c#
...
hpack.Encoder hpackEncoder = new hpack.Encoder(4096);
hpack.Decoder hpackDecoder = new hpack.Decoder(8192, 4096);

using(var binWriter = new BinaryWriter(new MemoryStream())) {
  hpackEncoder.EncodeHeader(binWriter, ":authority", "localhost:8080");
  hpackEncoder.EncodeHeader(binWriter, ":method", "GET");
  hpackEncoder.EncodeHeader(binWriter, ":path", "/");
  hpackEncoder.EncodeHeader(binWriter, ":scheme", "http");
  
  // send the data (binWriter) to the server!
  
  // wait for receiving headers...
  // for this example the encoded data in binWriter.
  var headerBlockFragment = new byte[binWriter.BaseStream.Length];
  binWriter.BaseStream.Position = 0;
  binWriter.BaseStream.Read(headerBlockFragment, 0, binWriter.BaseStream.Length);
  using(var binReader = new BinaryReader(new MemoryStream(headerBlockFragment))) {
    hpackDecoder.Decode(binReader, headerListener);
    hpackDecoder.EndHeaderBlock(); // this must be called to finalize the decoding process.
    
    // now the decoded headers should be printed from the defined "headerListener.HeaderEmitted" above.
  }
}
...
```
