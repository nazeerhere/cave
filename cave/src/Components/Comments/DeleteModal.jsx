import React from "react"
import axios from "axios"

export default function DeleteModal({ id }) {

    const closeModal = () => {
        document.getElementById("DeleteModal").style.opacity = 0
        document.getElementById("DeleteModal").style.zIndex = -1
    }
    
    const handleSubmit = () => {
        axios.delete(`https://uncool-backend.herokuapp.com/comments/${id}`)
        document.getElementById("DeleteModal").style.opacity = 0
        document.getElementById("DeleteModal").style.zIndex = -1
    }
    return(
        <div id="DeleteModal" >
            <button onClick={closeModal} className="closerBtn" >X</button>
            <span id="idInfo" >
                #{id}
            </span>
            <span id="deleteText" >
                <br/>
                are you sure you want to delete this comment?
                <br/>
                <button onClick={handleSubmit} id="Ddelete">Delete Comment</button>
            </span>
        </div>
    )
}