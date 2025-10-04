import './App.css'
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom"
import SPGeneratorPage from './pages/SpGen'
import AppLayout from './components/layout/Layout'

function App() {
  return (
    <BrowserRouter>
      <AppLayout>
        <Routes>
          <Route path="/" element={<Navigate to="/sp-generator" />} />
          <Route path="/sp-generator" element={<SPGeneratorPage />} />
          {/* <Route path="/patterns" element={<PatternsPage />} /> */}
          {/* <Route path="/history" element={<HistoryPage />} /> */}
          {/* <Route path="/settings" element={<SettingsPage />} /> */}
        </Routes>
      </AppLayout>
    </BrowserRouter>
  )
}

export default App
