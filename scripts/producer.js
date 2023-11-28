const fsP = require("node:fs/promises")
const rlP = require("node:readline/promises")

!(async () => {
    let lastId = 0
    const rl = rlP.createInterface({ input: process.stdin, output: process.stdout })

    rl.on("line", async value => {
        const id = ++lastId
        const data = { id, value }

        await fsP.appendFile("default.log", JSON.stringify(data) + "\n", { encoding: "utf-8" })
    })

    const handleSignal = signal => {
        rl.close()
        process.off("SIGINT", handleSignal)
        process.off("SIGTERM", handleSignal)
        process.kill(process.pid, signal)
    }
    process.on("SIGINT", handleSignal)
    process.on("SIGTERM", handleSignal)

})()

