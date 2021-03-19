import React, { useState } from "react"

export default function Filter() {
    const [filteredText, setFilter] = useState("")

    const handleChange = (event) => {
        const userInput = event.target.value
        setFilter(userInput)
    }

    const handleSubmit = (event) => {
        event.preventDefault()

    }
 
    return(
        <div  className="Filter">
                <input type="text"
                       id="filterInput"
                       placeholder="search by user..." 
                       onChange={handleChange}
                />    
                <button id="filterBtn"  
                        className="dropdown-toggle" 
                        onClick={handleSubmit}
                >
                FILTER
                </button>
        </div>
    )
}