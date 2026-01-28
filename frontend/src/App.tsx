import { useState } from "react";
import "./App.css";

type Msg = { role: "user" | "bot"; text: string };

function ThinkingIndicator() {
  return (
    <div className="chat-message bot thinking">
      <div className="thinking-content">
        <span className="thinking-text">Gemini denkt nach</span>
        <div className="thinking-dots">
          <span className="dot"></span>
          <span className="dot"></span>
          <span className="dot"></span>
        </div>
      </div>
    </div>
  );
}

export default function App() {
  const [input, setInput] = useState("");
  const [msgs, setMsgs] = useState<Msg[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  async function send() {
    const text = input.trim();
    if (!text || isLoading) return;

    setMsgs((m) => [...m, { role: "user", text }]);
    setInput("");
    setIsLoading(true);

    try {
      const res = await fetch("http://localhost:5087/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: text }),
      });

      const data = await res.json();
      setMsgs((m) => [...m, { role: "bot", text: data.reply }]);
    } catch (error) {
      setMsgs((m) => [...m, { role: "bot", text: "Fehler bei der Verbindung zum Server." }]);
    } finally {
      setIsLoading(false);
    }
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
        {isLoading && <ThinkingIndicator />}
      </div>

      <div className="chat-input-row">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && send()}
          placeholder="Nachricht..."
          disabled={isLoading}
        />
        <button onClick={send} disabled={isLoading}>
          {isLoading ? "..." : "Senden"}
        </button>
      </div>
    </div>
  );
}
