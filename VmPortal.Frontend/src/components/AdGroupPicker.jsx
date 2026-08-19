import { useEffect, useMemo, useRef, useState } from 'react'
import { searchAdGroups } from '../api/adminApi'
import { getErrorMessage } from '../api/errors'

const DEBOUNCE_MS = 300

/**
 * Sucht AD-Gruppen über GET /api/admin/ad-groups (Autocomplete), erlaubt eine Auswahl aber
 * nur, wenn die Gruppe auch als UserGroup in der Autorisierungs-DB existiert - POST
 * /api/admin/permissions braucht eine UserGroupId, die es für eine der Autorisierungs-DB
 * noch unbekannte AD-Gruppe nicht gibt (kein Endpunkt zum Anlegen neuer UserGroups
 * vorhanden, siehe Abschlussbericht). knownUserGroups (aus den bestehenden Zuordnungen
 * abgeleitet) ist deshalb die eigentliche Auswahlquelle; die Live-AD-Suche dient nur der
 * Rechtschreibhilfe/Discovery und als Fallback-Anzeige, wenn sie fehlschlägt (z. B. 502
 * ohne LDAP-Service-Account).
 */
function AdGroupPicker({ knownUserGroups, selectedUserGroupId, onSelect }) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [suggestions, setSuggestions] = useState([])
  const [searchError, setSearchError] = useState('')
  const debounceRef = useRef(null)

  const knownByLowerName = useMemo(() => {
    const map = new Map()
    knownUserGroups.forEach((g) => map.set(g.name.toLowerCase(), g))
    return map
  }, [knownUserGroups])

  useEffect(() => {
    if (!open) return undefined

    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      searchAdGroups(query || undefined)
        .then((response) => {
          setSuggestions(response.data.groups)
          setSearchError('')
        })
        .catch((err) => {
          setSuggestions([])
          setSearchError(getErrorMessage(err, 'AD-Gruppensuche aktuell nicht verfügbar.'))
        })
    }, DEBOUNCE_MS)

    return () => clearTimeout(debounceRef.current)
  }, [query, open])

  const fallbackNames = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return knownUserGroups
      .map((g) => g.name)
      .filter((name) => !needle || name.toLowerCase().includes(needle))
  }, [knownUserGroups, query])

  const displayedNames = searchError ? fallbackNames : suggestions
  const selectedName = knownUserGroups.find((g) => g.id === selectedUserGroupId)?.name ?? ''

  const handlePick = (name) => {
    const known = knownByLowerName.get(name.toLowerCase())
    if (!known) return
    onSelect(known.id)
    setQuery(known.name)
    setOpen(false)
  }

  return (
    <div className="ad-group-picker">
      <input
        type="text"
        value={open ? query : selectedName || query}
        onChange={(e) => {
          setQuery(e.target.value)
          setOpen(true)
          onSelect(null)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        placeholder="AD-Gruppe suchen…"
      />
      {open && (
        <div className="ad-group-dropdown">
          {searchError && <div className="ad-group-hint">{searchError} Zeige bekannte AD-Gruppen.</div>}
          {displayedNames.length === 0 && <div className="ad-group-hint">Keine Treffer.</div>}
          {displayedNames.map((name) => {
            const known = knownByLowerName.get(name.toLowerCase())
            return (
              <div
                key={name}
                className={`ad-group-option${known ? '' : ' disabled'}`}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handlePick(name)}
                title={
                  known
                    ? undefined
                    : 'Der Autorisierungs-DB noch nicht bekannt (keine UserGroup-Id) - eine Zuordnung ist damit aktuell nicht möglich.'
                }
              >
                {name}
                {!known && <span className="ad-group-unknown"> (nicht registriert)</span>}
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

export default AdGroupPicker
