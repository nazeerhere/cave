import React, { useState } from "react"

export default function Filter({ comments, filteredText, setFilter }) {
    let commentHolder = comments
    
    const handleChange = (event) => {
        let userInput = event.target.value
        userInput = parseFloat(userInput)
        if (userInput) {
            let filteredInput = comments.filter(daInput => {
                if (daInput.user == userInput) {
                    return daInput
                }
            })
            setFilter(filteredInput)
        } else {
            setFilter(commentHolder)
        }
        
        console.log(userInput)
    }

    const handleSubmit = (event) => {
        // filteredText.length ? setComment(filteredText) : setComment(name14)
        console.log(filteredText)
    }
 
    return(
        <div  className="Filter">
            <form>
                <input type="text"
                       id="filterInput"
                       placeholder="filter by user..." 
                       onChange={handleChange}
                />    
                <button id="filterBtn"  
                        className="dropdown-toggle" 
                        onClick={handleSubmit}
                >
                FILTER
                </button>
            </form>
        </div>
    )
}