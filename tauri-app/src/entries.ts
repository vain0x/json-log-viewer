export interface Entry {
  id: number
  title: string
  contents: string
}

export const parseEntries = (text: string): Entry[] => {
  const output: Entry[] = []
  const lines = text.split(/\r?\n/)
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index].trim()
    if (line.length === 0) continue

    let contents: string | null = null
    try {
      const data = JSON.parse(line)
      contents = JSON.stringify(data, null, 2)
      contents += "\n"
    } catch (err) {
      // pass.
    }
    if (contents == null) {
      contents = `#${index + 1}\n\n${line}\n`
    }

    output.push({ id: index + 1, title: line, contents })
  }
  return output
}
