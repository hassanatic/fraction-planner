import { FormEvent, useEffect, useState } from "react";
import { api, CourseView } from "./api";

export default function App() {
  const [courses, setCourses] = useState<CourseView[]>([]);
  const [selected, setSelected] = useState<CourseView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [patientRef, setPatientRef] = useState("");
  const [fractions, setFractions] = useState(5);
  const [startDate, setStartDate] = useState("");

  useEffect(() => {
    api.list().then(setCourses).catch((e: Error) => setError(e.message));
  }, []);

  async function run(action: () => Promise<CourseView>) {
    setError(null);
    try {
      const course = await action();
      setSelected(course);
      setCourses((all) => [...all.filter((c) => c.id !== course.id), course].sort((a, b) => a.patientRef.localeCompare(b.patientRef)));
    } catch (e) {
      setError((e as Error).message);
    }
  }

  function create(event: FormEvent) {
    event.preventDefault();
    run(() => api.create(patientRef, fractions, startDate));
  }

  const next = selected?.sessions.find((s) => s.status === "planned");

  return (
    <main>
      <h1>fraction-planner</h1>
      <form onSubmit={create}>
        <label>
          Patient reference
          <input value={patientRef} onChange={(e) => setPatientRef(e.target.value)} required />
        </label>
        <label>
          Fractions
          <input type="number" min={1} max={60} value={fractions} onChange={(e) => setFractions(Number(e.target.value))} />
        </label>
        <label>
          Start date
          <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
        </label>
        <button type="submit">Create course</button>
      </form>

      {error && <p role="alert">{error}</p>}

      <nav aria-label="Courses">
        {courses.map((c) => (
          <button key={c.id} onClick={() => setSelected(c)} aria-pressed={selected?.id === c.id}>
            {c.patientRef} ({c.status})
          </button>
        ))}
      </nav>

      {selected && (
        <section aria-label="Course">
          <h2>{selected.patientRef}</h2>
          <p>
            Delivered {selected.delivered} of {selected.prescribed}, missed {selected.missed}, {selected.status}
          </p>
          <table>
            <thead>
              <tr>
                <th>Session</th>
                <th>Date</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {selected.sessions.map((s) => (
                <tr key={s.number}>
                  <td>{s.number}</td>
                  <td>{s.date}</td>
                  <td>{s.status}</td>
                  <td>
                    {next?.number === s.number && (
                      <>
                        <button onClick={() => run(() => api.deliver(selected.id, s.number))}>Deliver fraction {s.number}</button>
                        <button onClick={() => run(() => api.miss(selected.id, s.number))}>Mark fraction {s.number} missed</button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </main>
  );
}
