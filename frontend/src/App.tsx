import { useState } from "react";
import "./App.css";

type Msg = { role: "user" | "bot"; text: string };

export default function App() {
  const [input, setInput] = useState("");
  const [msgs, setMsgs] = useState<Msg[]>([]);

  async function send() {
    const text = input.trim();
    if (!text) return;

    setMsgs((m) => [...m, { role: "user", text }]);
    setInput("");

    const res = await fetch("http://localhost:5087/api/chat", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message: text }),
    });

    const data = await res.json();
    setMsgs((m) => [...m, { role: "bot", text: data.reply }]);
  }

  return (
    <div className="chat-container">
      <h2>adessoGPT Mini</h2>

      <div className="chat-messages">
        {msgs.map((m, i) => (
          <div key={i} className={`chat-message ${m.role}`}>
            <b>{m.role === "user" ? "Du" : "Bot"}:</b> {m.text}
          </div>
        ))}
      </div>

      <div className="chat-input-row">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && send()}
          placeholder="Nachricht..."
        />
        <button onClick={send}>Senden</button>
      </div>
    </div>
  );
}
