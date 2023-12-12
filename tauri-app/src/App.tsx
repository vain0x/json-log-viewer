import React, { useMemo, useRef, useState } from "react"
import { Entry, parseEntries } from "./entries"

export default function App() {
  const [inputText, setInputText] = useState("")
  const [selectedEntry, setSelectedEntry] = useState<Entry | null>(null)

  const [entries, setEntries] = useState<Entry[]>(() => (
    [...new Array(100).keys()].map(i => ({ id: i + 1, title: `Item #${i + 1}`, contents: `# Item ${i + 1}\n\nContents. Contents.\n` }))
  ))

  const outputText = useMemo(() => {
    if (selectedEntry == null) {
      return "(no output...)"
    }
    return selectedEntry.contents
  }, [selectedEntry])

  // async function greet() {
  //   // Learn more about Tauri commands at https://tauri.app/v1/guides/features/command
  //   setGreetMsg(await invoke("greet", { name }))
  // }

  const handleFileChange = (ev: React.ChangeEvent<HTMLInputElement & { type: "file" }>) => {
    const input = ev.currentTarget
    const files = input.files

    if (input != null && files != null) {
      input.files = null
      for (let i = 0; i < files.length; i++) {
        const file = files[i]
        console.log("file =", file)
      }
    }
  }

  const timeoutRef = useRef<any>()
  const handleInputTextChange = (ev: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = ev.currentTarget.value
    setInputText(value)

    const t = timeoutRef.current
    if (t != null) {
      clearTimeout(t)
    }
    timeoutRef.current = setTimeout(() => {
      timeoutRef.current = null
      setEntries(parseEntries(value))
    }, 100)
  }

  const handleEntryList = (ev: React.MouseEvent<HTMLElement>) => {
    if (ev.defaultPrevented || ev.button !== 0 || (ev.altKey || ev.ctrlKey || ev.metaKey || ev.shiftKey)) {
      return
    }

    const item = (ev.target as HTMLElement | null)?.closest(".entry-list__item") as HTMLElement | null
    const dataId = item?.getAttribute("data-id")
    if (item == null || dataId == null) {
      return
    }

    const id = +dataId
    const entry = entries.find(e => e.id === id)
    console.log("entry selected", entry)
    if (entry == null) {
      return
    }

    ev.preventDefault()
    ev.stopPropagation()
    setSelectedEntry(entry)
  }

  return (
    <div data-jsname="App"
      style={{
        display: "grid",
        gridTemplateRows: "auto 1fr",
        gridTemplateColumns: "200px 1fr",
      }}>
      <form autoComplete="off"
        style={{
          gridColumn: "1 / 3",
          padding: "4px",
          height: "80px",
          display: "grid",
          gridTemplateColumns: "1fr auto",
          gap: "16px",
          backgroundColor: "#EBEBEB",
        }}
        onSubmit={ev => {
          ev.preventDefault()
          console.log("form submitted")
        }}>
        <textarea
          value={inputText}
          onChange={handleInputTextChange} />

        <label style={{
          display: "flex", alignItems: "center", gap: "4px",
        }}>
          <div>FILE:</div>
          <input type="file" accept=".log,.txt"
            placeholder="Select log file..."
            onChange={handleFileChange} />
        </label>
      </form>

      <div className="entry-list" style={{
        boxShadow: "inset 1px 1px 1px #C9C9C9",
        overflowX: "auto",
        overflowY: "scroll",
      }} onClick={handleEntryList}>
        <div className="entry-list__body" style={{
          width: "max-content",
          height: "max-content",
          display: "grid",
        }}>
          {entries.map(e => (
            <div key={e.id} data-id={e.id} data-selected={e === selectedEntry}
              className="entry-list__item" style={{
                padding: "2px 4px",
                height: "32px",
                wordBreak: "keep-all",
                cursor: "default",
              }}>
              {e.title}
            </div>
          ))}
        </div>
      </div>

      <div style={{
        padding: "4px",
        border: "1px solid #DBDBDB",
        backgroundColor: "#FCFCFC",
        whiteSpace: "pre-wrap",
        fontFamily: "monospace",
      }}>
        {outputText}
      </div>
    </div>
  )
}
