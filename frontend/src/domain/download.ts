export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** "Bar 1" → "bar_1" like the old client's export names. */
export function slug(name: string): string {
  return name.toLowerCase().trim().replace(/\s+/g, '_')
}
