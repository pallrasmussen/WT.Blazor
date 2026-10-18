namespace WT.Blazor.Services;

public sealed class ExportFile
{
	private readonly byte[]? content;
	private readonly Func<Stream, CancellationToken, Task>? writer;

	public ExportFile(byte[] content, string contentType, string fileName)
	{
		this.content = content;
		ContentType = contentType;
		FileName = fileName;
	}

	private ExportFile(string contentType, string fileName, Func<Stream, CancellationToken, Task> writer)
	{
		ContentType = contentType;
		FileName = fileName;
		this.writer = writer;
	}

	public string ContentType { get; }
	public string FileName { get; }
	public long? ContentLength => content?.LongLength;

	public static ExportFile FromWriter(string contentType, string fileName, Func<Stream, CancellationToken, Task> writer) =>
		new(contentType, fileName, writer);

	public Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default) =>
		content is not null
			? stream.WriteAsync(content, cancellationToken).AsTask()
			: writer!(stream, cancellationToken);

	public async Task<byte[]> ReadContentAsync(CancellationToken cancellationToken = default)
	{
		await using var stream = new MemoryStream();
		await WriteToAsync(stream, cancellationToken);
		return stream.ToArray();
	}
}