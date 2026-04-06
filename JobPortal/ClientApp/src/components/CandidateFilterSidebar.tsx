import React, { useState } from "react";

interface FacetItem {
    value: string;
    label?: string;
    count: number;
}

interface CandidateSearchParams {
    searchQuery?: string;
    locations?: string[];
    jobIds?: number[];
    sort?: string;
    dir?: string;
    pageSize?: number;
}

interface CandidateFiltersData {
    params: CandidateSearchParams;
    locationFacets: FacetItem[];
    jobFacets: FacetItem[];
}

export default function CandidateFilterSidebar({ initial }: { initial: CandidateFiltersData }) {
    const [searchQuery, setSearchQuery] = useState(initial.params.searchQuery || "");
    const [selectedLocations, setSelectedLocations] = useState<string[]>(initial.params.locations || []);
    const [selectedJobs, setSelectedJobs] = useState<number[]>(initial.params.jobIds || []);
    const [locationSearch, setLocationSearch] = useState("");
    const [showAllLocations, setShowAllLocations] = useState(false);

    const filteredLocations = initial.locationFacets.filter(f =>
        f.value.toLowerCase().includes(locationSearch.toLowerCase())
    );
    const visibleLocations = showAllLocations
        ? filteredLocations
        : filteredLocations.slice(0, 5);

    const toggleLocation = (value: string) => {
        setSelectedLocations(prev =>
            prev.includes(value) ? prev.filter(x => x !== value) : [...prev, value]
        );
    };

    const toggleJob = (value: number) => {
        setSelectedJobs(prev =>
            prev.includes(value) ? prev.filter(x => x !== value) : [...prev, value]
        );
    };

    const handleSubmit = () => {
        const params = new URLSearchParams();

        if (searchQuery) params.append("SearchQuery", searchQuery);

        selectedLocations.forEach(l => params.append("Locations", l));
        selectedJobs.forEach(j => params.append("JobIds", j.toString()));

        params.append("Sort", initial.params.sort || "date");
        params.append("Dir", initial.params.dir || "desc");
        params.append("PageSize", String(initial.params.pageSize || 25));
        params.append("Page", "1");

        window.location.href = `/Admin/Candidates?${params.toString()}`;
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === "Enter") {
            e.preventDefault();
            handleSubmit();
        }
    };

    const hasFilters = searchQuery || selectedLocations.length > 0 || selectedJobs.length > 0;

    return (
        <div className="card shadow-sm border-0">
            <div className="card-header" style={{ background: "linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%)" }}>
                <h5 className="fw-bold mb-0 text-white">
                    <i className="fas fa-filter me-2"></i>Filters
                </h5>
            </div>
            <div className="card-body">
                {/* Search */}
                <div className="mb-3">
                    <label className="form-label fw-semibold small text-muted">
                        <i className="fas fa-search me-1"></i>Search
                    </label>
                    <input
                        type="text"
                        className="form-control form-control-sm"
                        value={searchQuery}
                        onChange={e => setSearchQuery(e.target.value)}
                        onKeyDown={handleKeyDown}
                        placeholder="Name or email..."
                    />
                </div>

                {/* Location facets */}
                {initial.locationFacets.length > 0 && (
                    <div className="mb-3">
                        <label className="form-label fw-semibold small text-muted">
                            <i className="fas fa-map-marker-alt me-1"></i>Location
                        </label>
                        <input
                            type="text"
                            className="form-control form-control-sm mb-2"
                            placeholder="Search locations..."
                            value={locationSearch}
                            onChange={e => setLocationSearch(e.target.value)}
                        />
                        <div style={{ maxHeight: "200px", overflowY: "auto" }}>
                            {visibleLocations.map(f => (
                                <div className="form-check" key={f.value}>
                                    <input
                                        className="form-check-input"
                                        type="checkbox"
                                        id={`loc-${f.value}`}
                                        checked={selectedLocations.includes(f.value)}
                                        onChange={() => toggleLocation(f.value)}
                                    />
                                    <label className="form-check-label small" htmlFor={`loc-${f.value}`}>
                                        {f.value} <span className="badge bg-secondary ms-1">{f.count}</span>
                                    </label>
                                </div>
                            ))}
                        </div>
                        {filteredLocations.length > 5 && (
                            <button
                                type="button"
                                className="btn btn-link btn-sm p-0 mt-1 text-decoration-none"
                                onClick={() => setShowAllLocations(!showAllLocations)}
                            >
                                {showAllLocations ? "Show Less" : `Show More (${filteredLocations.length - 5})`}
                            </button>
                        )}
                    </div>
                )}

                {/* Job facets */}
                {initial.jobFacets.length > 0 && (
                    <div className="mb-3">
                        <label className="form-label fw-semibold small text-muted">
                            <i className="fas fa-briefcase me-1"></i>Job
                        </label>
                        <div style={{ maxHeight: "200px", overflowY: "auto" }}>
                            {initial.jobFacets.map((f, idx) => (
                                <div className="form-check" key={f.value}>
                                    <input
                                        className="form-check-input"
                                        type="checkbox"
                                        id={`job-${idx}`}
                                        checked={selectedJobs.includes(Number(f.value))}
                                        onChange={() => toggleJob(Number(f.value))}
                                    />
                                    <label className="form-check-label small" htmlFor={`job-${idx}`}>
                                        {f.label || f.value} <span className="badge bg-secondary ms-1">{f.count}</span>
                                    </label>
                                </div>
                            ))}
                        </div>
                    </div>
                )}

                {/* Submit */}
                <button
                    className="btn btn-sm w-100"
                    style={{ background: "linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%)", color: "#fff", border: "none" }}
                    onClick={handleSubmit}
                >
                    <i className="fas fa-check me-1"></i>Apply Filters
                </button>

                {hasFilters && (
                    <a
                        href="/Admin/Candidates"
                        className="btn btn-sm btn-outline-secondary w-100 mt-2"
                    >
                        <i className="fas fa-times me-1"></i>Clear All
                    </a>
                )}
            </div>
        </div>
    );
}
